using System.Threading.RateLimiting;
using GuestEvents.Data;
using GuestEvents.Models;
using GuestEvents.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var provider = builder.Configuration["Db:Provider"] ?? "Sqlite";
builder.Services.AddDbContext<AppDb>(o =>
{
    var cs = builder.Configuration.GetConnectionString("Default") ?? "Data Source=App_Data/events.db";
    if (provider == "SqlServer") o.UseSqlServer(cs); else o.UseSqlite(cs);
});

builder.Services.AddControllersWithViews(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Loc>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<MailService>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<QrService>();
builder.Services.AddSingleton<DeviceService>();
// Keys that encrypt the saved LDAP/SMTP passwords live in App_Data/keys (back this folder up with the database).
var dp = builder.Services.AddDataProtection().SetApplicationName("GuestEvents")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));
if (OperatingSystem.IsWindows()) dp.ProtectKeysWithDpapi();
builder.Services.AddScoped<ConnectionConfig>();
builder.Services.AddScoped<LdapAuthService>();
if ((builder.Configuration["Auth:Mode"] ?? "Ldap") == "Dev") builder.Services.AddSingleton<IAuthService, DevAuthService>();
else builder.Services.AddScoped<IAuthService>(sp => sp.GetRequiredService<LdapAuthService>());
builder.Services.AddHostedService<TickService>();

var authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/Denied";
        o.ExpireTimeSpan = TimeSpan.FromMinutes(30);   // inactivity timeout
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.Name = "ge_auth";
    });
// Windows single sign-on (Kerberos / NTLM). Only works when the server itself is joined to the domain.
if (builder.Configuration.GetValue("Auth:Sso", false)) authBuilder.AddNegotiate();
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Requester", p => p.RequireRole(Roles.Staff, Roles.Admin));
    o.AddPolicy("Hod", p => p.RequireRole(Roles.Hod));
    o.AddPolicy("Admin", p => p.RequireRole(Roles.Admin));
    o.AddPolicy("Viewer", p => p.RequireRole(Roles.Staff, Roles.Admin, Roles.Hod, Roles.Media, Roles.Research));
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("Auth:LoginsPerMinutePerIp", 20), Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("public", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("Public:RequestsPerMinutePerIp", 60), Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("publicPost", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("Public:SubmitsPerMinutePerIp", 30), Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownNetworks.Clear(); o.KnownProxies.Clear(); // trusted reverse proxy only; restrict at the network level
});

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.EnsureCreated();
    // EnsureCreated does not add tables to an existing database, so add newer ones here.
    if (provider == "SqlServer")
        db.Database.ExecuteSqlRaw("IF OBJECT_ID(N'RoleAssignments') IS NULL CREATE TABLE RoleAssignments (Id INT IDENTITY PRIMARY KEY, Username NVARCHAR(100) NOT NULL, Role NVARCHAR(20) NOT NULL, AssignedBy NVARCHAR(MAX) NOT NULL, AssignedAt DATETIME2 NOT NULL); IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_RoleAssignments_Username_Role') CREATE UNIQUE INDEX IX_RoleAssignments_Username_Role ON RoleAssignments(Username, Role)");
    if (provider == "SqlServer")
    {
        db.Database.ExecuteSqlRaw("IF COL_LENGTH('Events','EndDate') IS NULL ALTER TABLE Events ADD EndDate DATETIME2 NULL");
        db.Database.ExecuteSqlRaw("IF COL_LENGTH('Attendances','Day') IS NULL BEGIN ALTER TABLE Attendances ADD Day DATETIME2 NOT NULL DEFAULT '0001-01-01'; EXEC('UPDATE a SET Day = e.Date FROM Attendances a JOIN Events e ON e.Id = a.EventRequestId') END");
    }
    else
    {
        if (db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Events') WHERE name='EndDate'").AsEnumerable().First() == 0)
            db.Database.ExecuteSqlRaw("ALTER TABLE Events ADD COLUMN EndDate TEXT NULL");
        if (db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Attendances') WHERE name='Day'").AsEnumerable().First() == 0)
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE Attendances ADD COLUMN Day TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");
            db.Database.ExecuteSqlRaw("UPDATE Attendances SET Day = (SELECT Date FROM Events WHERE Events.Id = Attendances.EventRequestId)");
        }
    }
    if (provider != "SqlServer")
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS RoleAssignments (Id INTEGER PRIMARY KEY AUTOINCREMENT, Username TEXT NOT NULL, Role TEXT NOT NULL, AssignedBy TEXT NOT NULL, AssignedAt TEXT NOT NULL); CREATE UNIQUE INDEX IF NOT EXISTS IX_RoleAssignments_Username_Role ON RoleAssignments(Username, Role)");
    // ExternalVisits is newer than the first release, so create it on existing databases too.
    if (provider == "SqlServer")
        db.Database.ExecuteSqlRaw("IF OBJECT_ID(N'Visits') IS NULL CREATE TABLE Visits (Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(200) NOT NULL, Destination NVARCHAR(200) NOT NULL, DestinationAddress NVARCHAR(500) NULL, Purpose NVARCHAR(MAX) NULL, Date DATETIME2 NOT NULL, DepartureTime TIME NOT NULL, ReturnTime TIME NOT NULL, PickupLocation NVARCHAR(200) NOT NULL, Grade NVARCHAR(100) NULL, StudentCount INT NOT NULL, StaffCount INT NOT NULL, SupervisorName NVARCHAR(200) NOT NULL, SupervisorPhone NVARCHAR(50) NOT NULL, Notes NVARCHAR(MAX) NULL, Status INT NOT NULL, RequesterUsername NVARCHAR(MAX) NOT NULL, RequesterName NVARCHAR(MAX) NOT NULL, RequesterDepartment NVARCHAR(MAX) NULL, RequesterEmail NVARCHAR(MAX) NULL, CreatedAt DATETIME2 NOT NULL, SubmittedAt DATETIME2 NULL, DecisionBy NVARCHAR(MAX) NULL, DecisionComment NVARCHAR(MAX) NULL, DecidedAt DATETIME2 NULL, BusDetails NVARCHAR(MAX) NULL)");
    else
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS Visits (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Destination TEXT NOT NULL, DestinationAddress TEXT NULL, Purpose TEXT NULL, Date TEXT NOT NULL, DepartureTime TEXT NOT NULL, ReturnTime TEXT NOT NULL, PickupLocation TEXT NOT NULL, Grade TEXT NULL, StudentCount INTEGER NOT NULL, StaffCount INTEGER NOT NULL, SupervisorName TEXT NOT NULL, SupervisorPhone TEXT NOT NULL, Notes TEXT NULL, Status INTEGER NOT NULL, RequesterUsername TEXT NOT NULL, RequesterName TEXT NOT NULL, RequesterDepartment TEXT NULL, RequesterEmail TEXT NULL, CreatedAt TEXT NOT NULL, SubmittedAt TEXT NULL, DecisionBy TEXT NULL, DecisionComment TEXT NULL, DecidedAt TEXT NULL, BusDetails TEXT NULL)");
    if (provider == "SqlServer")
        db.Database.ExecuteSqlRaw("IF OBJECT_ID(N'ResearchDocs') IS NULL CREATE TABLE ResearchDocs (Id INT IDENTITY PRIMARY KEY, Title NVARCHAR(300) NOT NULL, DocType NVARCHAR(50) NOT NULL, Authors NVARCHAR(300) NULL, Summary NVARCHAR(MAX) NULL, OriginalFileName NVARCHAR(MAX) NOT NULL, FilePath NVARCHAR(MAX) NOT NULL, FileSize BIGINT NOT NULL, UploaderUsername NVARCHAR(MAX) NOT NULL, UploaderName NVARCHAR(MAX) NOT NULL, UploaderDepartment NVARCHAR(MAX) NULL, UploaderEmail NVARCHAR(MAX) NULL, UploadedAt DATETIME2 NOT NULL, EmailSent BIT NOT NULL)");
    else
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS ResearchDocs (Id INTEGER PRIMARY KEY AUTOINCREMENT, Title TEXT NOT NULL, DocType TEXT NOT NULL, Authors TEXT NULL, Summary TEXT NULL, OriginalFileName TEXT NOT NULL, FilePath TEXT NOT NULL, FileSize INTEGER NOT NULL, UploaderUsername TEXT NOT NULL, UploaderName TEXT NOT NULL, UploaderDepartment TEXT NULL, UploaderEmail TEXT NULL, UploadedAt TEXT NOT NULL, EmailSent INTEGER NOT NULL)");
    var s = await scope.ServiceProvider.GetRequiredService<SettingsService>().AllAsync();
    Loc.UseArabicDigits = SettingsService.Bool(s, "UseArabicDigits");
    Loc.DateFmt = s["DateFormat"];
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();

// One app, two faces: on the public host ONLY the check-in/evaluation pages and static files are reachable.
var publicHost = app.Configuration["App:PublicHost"];
app.Use(async (ctx, next) =>
{
    if (!string.IsNullOrEmpty(publicHost) && ctx.Request.Host.Host.Equals(publicHost, StringComparison.OrdinalIgnoreCase))
    {
        var p = ctx.Request.Path;
        var ok = p.StartsWithSegments("/p") || p.StartsWithSegments("/css") || p.StartsWithSegments("/js") || p.StartsWithSegments("/lib") || p.StartsWithSegments("/lang") || p.Value == "/favicon.ico";
        if (!ok) { ctx.Response.StatusCode = 404; return; }
    }
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Events}/{action=Index}/{id?}");
app.Run();

/// <summary>Background job: HOD reminders (Sun-Thu working days) and status roll-over.</summary>
public class TickService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<TickService> _log;
    public TickService(IServiceProvider sp, ILogger<TickService> log) { _sp = sp; _log = log; }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { using var scope = _sp.CreateScope(); await scope.ServiceProvider.GetRequiredService<WorkflowService>().Tick(); }
            catch (Exception ex) { _log.LogError(ex, "Tick failed"); }
            await Task.Delay(TimeSpan.FromMinutes(10), ct);
        }
    }
}
