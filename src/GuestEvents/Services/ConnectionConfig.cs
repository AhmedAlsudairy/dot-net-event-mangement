using GuestEvents.Data;
using GuestEvents.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Services;

public record LdapOpts(string Host, int Port, bool UseSsl, bool SkipCert, string BaseDn, string SvcDn, string SvcPassword,
    string GroupStaff, string GroupHod, string GroupMedia, string GroupAdmin,
    bool StartTls = false, string CertThumbprint = "", bool Nested = true, string GroupResearch = "Research-InCharge");

public record SmtpOpts(bool Enabled, string Host, int Port, bool StartTls, string User, string Password, string From);

/// <summary>
/// LDAP/SMTP connection settings. Values saved on the Admin "Connections" page (table Settings, keys "Ldap:*" / "Smtp:*")
/// override appsettings.json. Passwords are stored encrypted with ASP.NET Data Protection ("enc:" prefix).
/// </summary>
public class ConnectionConfig
{
    private readonly AppDb _db;
    private readonly IConfiguration _cfg;
    private readonly IDataProtector _protector;
    public ConnectionConfig(AppDb db, IConfiguration cfg, IDataProtectionProvider dp) { _db = db; _cfg = cfg; _protector = dp.CreateProtector("GuestEvents.Connections.v1"); }

    public static readonly string[] Secrets = { "Ldap:ServiceAccountPassword", "Smtp:Password" };

    private async Task<Dictionary<string, string>> Stored() =>
        await _db.Settings.AsNoTracking().Where(s => s.Key.StartsWith("Ldap:") || s.Key.StartsWith("Smtp:")).ToDictionaryAsync(s => s.Key, s => s.Value);

    private string Get(Dictionary<string, string> st, string key, string fallback = "")
    {
        if (st.TryGetValue(key, out var v))
        {
            if (!v.StartsWith("enc:")) return v;
            try { return _protector.Unprotect(v[4..]); } catch { return ""; } // key ring lost => treat as unset
        }
        return _cfg[key] ?? fallback;
    }

    private static bool B(string v, bool d) => bool.TryParse(v, out var b) ? b : d;
    private static int I(string v, int d) => int.TryParse(v, out var i) ? i : d;

    public async Task<LdapOpts> LdapAsync()
    {
        var st = await Stored();
        return new LdapOpts(Get(st, "Ldap:Host"), I(Get(st, "Ldap:Port", "636"), 636), B(Get(st, "Ldap:UseSsl", "true"), true), B(Get(st, "Ldap:SkipCertValidation", "false"), false),
            Get(st, "Ldap:BaseDn"), Get(st, "Ldap:ServiceAccountDn"), Get(st, "Ldap:ServiceAccountPassword"),
            Get(st, "Ldap:GroupStaff", "Events-Staff"), Get(st, "Ldap:GroupHod", "Events-HOD"), Get(st, "Ldap:GroupMedia", "Events-Media"), Get(st, "Ldap:GroupAdmin", "Events-Admin"),
            B(Get(st, "Ldap:StartTls", "false"), false), Get(st, "Ldap:CertThumbprint"), B(Get(st, "Ldap:Nested", "true"), true), Get(st, "Ldap:GroupResearch", "Research-InCharge"));
    }

    public async Task<SmtpOpts> SmtpAsync()
    {
        var st = await Stored();
        return new SmtpOpts(B(Get(st, "Smtp:Enabled", "false"), false), Get(st, "Smtp:Host"), I(Get(st, "Smtp:Port", "25"), 25), B(Get(st, "Smtp:UseStartTls", "true"), true),
            Get(st, "Smtp:User"), Get(st, "Smtp:Password"), Get(st, "Smtp:From", "events@example.com"));
    }

    /// <summary>Saves one value. Secrets are encrypted; an empty secret means "keep the current one" and is skipped.</summary>
    public async Task SetAsync(string key, string value)
    {
        if (Secrets.Contains(key))
        {
            if (string.IsNullOrEmpty(value)) return;
            value = "enc:" + _protector.Protect(value);
        }
        var row = await _db.Settings.FindAsync(key);
        if (row == null) _db.Settings.Add(new AppSetting { Key = key, Value = value });
        else row.Value = value;
        await _db.SaveChangesAsync();
    }

    public async Task ClearAsync(string key)
    {
        var row = await _db.Settings.FindAsync(key);
        if (row != null) { _db.Settings.Remove(row); await _db.SaveChangesAsync(); }
    }
}
