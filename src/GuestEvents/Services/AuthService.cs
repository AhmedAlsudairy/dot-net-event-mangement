using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using GuestEvents.Models;
using Novell.Directory.Ldap;

namespace GuestEvents.Services;

public record AdUser(string Username, string DisplayName, string? Email, string? Department, List<string> Roles, List<string>? Groups = null);

public enum AuthFail { None, Invalid, Disabled, Locked, PasswordExpired, AccountExpired, Ambiguous, Unavailable, TooMany, NoRole }

public record AuthResult(AdUser? User, AuthFail Fail = AuthFail.None, string? Detail = null)
{
    public static AuthResult Ok(AdUser u) => new(u);
    public static AuthResult Failed(AuthFail f, string? detail = null) => new(null, f, detail);
}

public record GroupPerson(string Name, string? Email, string Sam);

/// <summary>One group in the "who is in this group" tree: its direct members and the groups inside it.</summary>
public class GroupNode
{
    public string Name { get; set; } = "";
    public string Dn { get; set; } = "";
    public List<GroupPerson> Users { get; set; } = new();
    public List<GroupNode> Children { get; set; } = new();
    /// <summary>This group was already shown higher up (groups can contain each other).</summary>
    public bool Cycle { get; set; }
    /// <summary>The tree was cut short (too many groups or members) - the list is not complete.</summary>
    public bool Truncated { get; set; }
}

public interface IAuthService
{
    Task<AuthResult> AuthenticateAsync(string username, string password);
    /// <summary>Finds a user WITHOUT checking a password (used for Windows single sign-on, where Windows already proved who they are).</summary>
    Task<AuthResult> LookupAsync(string login);
}

/// <summary>
/// Active Directory sign-in over LDAP. The app never stores passwords: it finds the user with a read-only service account,
/// then proves the password by binding as that user. Handles nested groups, several servers, UPN / DOMAIN\user / email logins,
/// locked / disabled / expired accounts, duplicate accounts, LDAP injection, certificate pinning and timeouts.
/// </summary>
public class LdapAuthService : IAuthService
{
    private readonly ConnectionConfig _cfg;
    private readonly ILogger<LdapAuthService> _log;
    public LdapAuthService(ConnectionConfig cfg, ILogger<LdapAuthService> log) { _cfg = cfg; _log = log; }

    private sealed class CertInfo { public string? Thumbprint; }

    // ------------------------------------------------------------------ public API
    public async Task<AuthResult> AuthenticateAsync(string username, string password)
    {
        // An empty password would turn an LDAP simple bind into an ANONYMOUS bind that "succeeds": never allow it.
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)
            || username.Length > 256 || password.Length > 512 || username.Contains('\0') || password.Contains('\0'))
            return AuthResult.Failed(AuthFail.Invalid);
        return await WithTimeout(o => FindAsync(o, username.Trim(), password));
    }

    public Task<AuthResult> LookupAsync(string login) =>
        string.IsNullOrWhiteSpace(login) || login.Length > 256 || login.Contains('\0')
            ? Task.FromResult(AuthResult.Failed(AuthFail.Invalid))
            : WithTimeout(o => FindAsync(o, login.Trim(), null));

    /// <summary>Admin "Test LDAP" with no user: reach a server, bind as the service account, check the four role groups.</summary>
    public async Task<(bool ok, string message)> TestConnectionAsync(LdapOpts o)
    {
        var info = new CertInfo();
        try
        {
            var (svc, server) = await ConnectAsync(o, info);
            using (svc)
            {
                await svc.BindAsync(o.SvcDn, o.SvcPassword);
                var parts = new List<string>();
                foreach (var g in new[] { o.GroupStaff, o.GroupHod, o.GroupMedia, o.GroupAdmin, o.GroupResearch }.SelectMany(MailService.Split).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var res = await svc.SearchAsync(o.BaseDn, LdapConnection.ScopeSub, $"(&(objectClass=group)(cn={Esc(g)}))", new[] { "cn" }, false);
                    bool found = false;
                    while (await res.HasMoreAsync()) { try { await res.NextAsync(); found = true; break; } catch (LdapReferralException) { } }
                    parts.Add($"{g} {(found ? "found" : "NOT FOUND")}");
                }
                var tls = o.UseSsl ? "LDAPS" : o.StartTls ? "StartTLS" : "NO encryption";
                var users = await SampleUsersAsync(svc, o);
                return (true, $"Connected to {server} ({tls}) and signed in as the service account. Groups: {string.Join(", ", parts)}. {users}" +
                              (info.Thumbprint != null ? $" Server certificate thumbprint: {info.Thumbprint}." : ""));
            }
        }
        catch (Exception ex) { return (false, Explain(ex, info)); }
    }

    /// <summary>
    /// Readiness check for email sign-in: is the Base DN readable, how many users (sample of up to 1000) have an email, and do any two share one.
    /// Two people with the same email cannot sign in with it, and people with no email can only use their username.
    /// </summary>
    private static async Task<string> SampleUsersAsync(LdapConnection svc, LdapOpts o)
    {
        const int Max = 1000;
        try
        {
            try
            {
                var b = await svc.SearchAsync(o.BaseDn, LdapConnection.ScopeBase, "(objectClass=*)", new[] { "distinguishedName" }, false);
                while (await b.HasMoreAsync()) { try { await b.NextAsync(); break; } catch (LdapReferralException) { } }
            }
            catch (LdapException ex) when (ex.ResultCode == LdapException.NoSuchObject) { return $"The Base DN \"{o.BaseDn}\" was not found - check it."; }

            var res = await svc.SearchAsync(o.BaseDn, LdapConnection.ScopeSub, "(&(objectClass=user)(!(objectClass=computer)))", new[] { "mail" }, false);
            int total = 0, withMail = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var dup = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            while (total < Max)
            {
                try
                {
                    if (!await res.HasMoreAsync()) break;
                    var e = await res.NextAsync(); total++;
                    var a = e.GetAttributeSet();
                    var m = a.ContainsKey("mail") ? a["mail"].StringValue?.Trim() : null;
                    if (string.IsNullOrEmpty(m)) continue;
                    withMail++;
                    if (!seen.Add(m)) dup.Add(m);
                }
                catch (LdapReferralException) { }
            }
            if (total == 0) return "No user accounts were found under the Base DN - check the Base DN and the service account's read rights.";
            var s = $"Users: {total}{(total >= Max ? "+ (sampled)" : "")}, {withMail} with an email address";
            if (withMail < total) s += $" ({total - withMail} without one can sign in only with their username)";
            s += dup.Count == 0 ? ", no duplicate emails." : $". Duplicate emails (these people cannot sign in by email): {string.Join(", ", dup.Take(5))}{(dup.Count > 5 ? ", ..." : "")}.";
            return s;
        }
        catch (Exception ex) { return $"Could not read users: {ex.Message}"; }
    }

    /// <summary>
    /// Admin "Members": the people in a group and, when nested groups are on, in every group inside it (loops are safe, depth and size are capped).
    /// </summary>
    public async Task<(bool ok, string message, GroupNode? tree)> GroupTreeAsync(LdapOpts o, string group)
    {
        var info = new CertInfo();
        try
        {
            var (svc, _) = await ConnectAsync(o, info);
            using (svc)
            {
                await svc.BindAsync(o.SvcDn, o.SvcPassword);
                var res = await svc.SearchAsync(o.BaseDn, LdapConnection.ScopeSub, $"(&(objectClass=group)(cn={Esc(group)}))", new[] { "cn" }, false);
                string? dn = null;
                while (dn == null)
                {
                    try { if (!await res.HasMoreAsync()) break; dn = (await res.NextAsync()).Dn; } catch (LdapReferralException) { }
                }
                if (dn == null) return (false, $"Group \"{group}\" was not found in the directory.", null);
                var budget = new[] { 150 }; // groups we are willing to open
                return (true, "", await ExpandAsync(svc, o, dn, group, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase), budget));
            }
        }
        catch (Exception ex) { return (false, Explain(ex, info), null); }
    }

    private static string? Val(LdapAttributeSet a, string k) => a.ContainsKey(k) ? a[k].StringValue?.Trim() : null;

    private static async Task<GroupNode> ExpandAsync(LdapConnection svc, LdapOpts o, string dn, string name, int depth, HashSet<string> visited, int[] budget)
    {
        var node = new GroupNode { Name = name, Dn = dn };
        visited.Add(dn); budget[0]--;
        var esc = Esc(dn);
        var res = await svc.SearchAsync(o.BaseDn, LdapConnection.ScopeSub, $"(&(objectClass=user)(!(objectClass=computer))(memberOf={esc}))", new[] { "sAMAccountName", "displayName", "mail" }, false);
        while (true)
        {
            if (node.Users.Count >= 500) { node.Truncated = true; break; }
            try
            {
                if (!await res.HasMoreAsync()) break;
                var a = (await res.NextAsync()).GetAttributeSet();
                var sam = Val(a, "sAMAccountName") ?? "";
                node.Users.Add(new GroupPerson(Val(a, "displayName") ?? sam, Val(a, "mail"), sam));
            }
            catch (LdapReferralException) { }
        }
        node.Users.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));
        if (!o.Nested || depth >= 6) return node;

        var kids = new List<(string dn, string name)>();
        var cres = await svc.SearchAsync(o.BaseDn, LdapConnection.ScopeSub, $"(&(objectClass=group)(memberOf={esc}))", new[] { "cn" }, false);
        while (kids.Count < 50)
        {
            try
            {
                if (!await cres.HasMoreAsync()) break;
                var e = await cres.NextAsync();
                kids.Add((e.Dn, Val(e.GetAttributeSet(), "cn") ?? e.Dn));
            }
            catch (LdapReferralException) { }
        }
        foreach (var k in kids.OrderBy(k => k.name, StringComparer.OrdinalIgnoreCase))
        {
            if (visited.Contains(k.dn)) { node.Children.Add(new GroupNode { Name = k.name, Dn = k.dn, Cycle = true }); continue; }
            if (budget[0] <= 0) { node.Truncated = true; break; }
            node.Children.Add(await ExpandAsync(svc, o, k.dn, k.name, depth + 1, visited, budget));
        }
        return node;
    }

    /// <summary>Admin "Fetch groups": every group name under the Base DN, so the admin can pick one for each role instead of typing it.</summary>
    public async Task<(bool ok, string message, List<string> groups)> ListGroupsAsync(LdapOpts o)
    {
        const int Max = 2000;
        var info = new CertInfo();
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var (svc, server) = await ConnectAsync(o, info);
            using (svc)
            {
                await svc.BindAsync(o.SvcDn, o.SvcPassword);
                var res = await svc.SearchAsync(o.BaseDn, LdapConnection.ScopeSub, "(objectClass=group)", new[] { "cn" }, false);
                while (names.Count < Max)
                {
                    try
                    {
                        if (!await res.HasMoreAsync()) break;
                        var e = await res.NextAsync();
                        var a = e.GetAttributeSet();
                        if (a.ContainsKey("cn")) names.Add(a["cn"].StringValue);
                    }
                    catch (LdapReferralException) { } // groups of other domains are skipped
                }
                var more = names.Count >= Max ? $" Showing the first {Max}; type the rest by hand." : "";
                return (true, $"Found {names.Count} group(s) on {server}.{more}", names.ToList());
            }
        }
        catch (Exception ex) { return (false, Explain(ex, info), new List<string>()); }
    }

    /// <summary>Admin "Test LDAP" with a user: full sign-in with the (unsaved) settings. Nothing is stored.</summary>
    public async Task<(bool ok, string message)> TestLoginAsync(LdapOpts o, string login, string password)
    {
        if (string.IsNullOrEmpty(password)) return (false, "Enter the test user's password too.");
        var r = await FindAsync(o, login.Trim(), password);
        if (r.User == null) return (false, Describe(r));
        var u = r.User;
        var roles = u.Roles.Count == 0 ? "none from AD groups (assign a role on the Roles page)" : string.Join(", ", u.Roles);
        return (true, $"OK: {u.DisplayName} ({u.Email ?? "no email"}). AD groups found: {(u.Groups is { Count: > 0 } ? string.Join(", ", u.Groups) : "none")}" +
                      $"{(o.Nested ? " (including groups inside groups)" : " (direct only)")}. Roles: {roles}.");
    }

    public static string Describe(AuthResult r) => r.Fail switch
    {
        AuthFail.Invalid => "Wrong username or password" + (r.Detail != null ? $" ({r.Detail})" : "") + ".",
        AuthFail.Disabled => "The account is disabled in Active Directory.",
        AuthFail.Locked => "The account is locked out in Active Directory.",
        AuthFail.PasswordExpired => "The password has expired or must be changed before signing in.",
        AuthFail.AccountExpired => "The account has expired in Active Directory.",
        AuthFail.Ambiguous => "More than one account matches this name - sign-in refused. Use DOMAIN\\user or the full user@domain name, or ask IT to remove the duplicate.",
        AuthFail.Unavailable => r.Detail ?? "The directory is not reachable.",
        _ => r.Detail ?? "Sign-in failed.",
    };

    // ------------------------------------------------------------------ core
    private async Task<AuthResult> WithTimeout(Func<LdapOpts, Task<AuthResult>> work)
    {
        var o = await _cfg.LdapAsync();
        if (string.IsNullOrWhiteSpace(o.Host)) return AuthResult.Failed(AuthFail.Unavailable, "The LDAP server is not configured.");
        var task = work(o);
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(25))) != task)
            return AuthResult.Failed(AuthFail.Unavailable, "The directory did not answer in time.");
        return await task;
    }

    private async Task<AuthResult> FindAsync(LdapOpts o, string login, string? password)
    {
        var info = new CertInfo();
        try
        {
            var (svc, _) = await ConnectAsync(o, info);
            using (svc)
            {
                try { await svc.BindAsync(o.SvcDn, o.SvcPassword); }
                catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
                { return AuthResult.Failed(AuthFail.Unavailable, "The service account was rejected (wrong DN or password) - check LDAP & Email."); }

                // up to two matches are enough to detect duplicates; referrals (other domains) are skipped, not followed
                var res = await svc.SearchAsync(o.BaseDn, LdapConnection.ScopeSub, BuildFilter(login),
                    new[] { "sAMAccountName", "displayName", "mail", "department", "memberOf", "userAccountControl", "accountExpires" }, false);
                var found = new List<LdapEntry>();
                while (found.Count < 2)
                {
                    try { if (!await res.HasMoreAsync()) break; found.Add(await res.NextAsync()); }
                    catch (LdapReferralException) { }
                }
                if (found.Count == 0) return AuthResult.Failed(AuthFail.Invalid, "user not found");
                if (found.Count > 1) return AuthResult.Failed(AuthFail.Ambiguous);
                var e = found[0];

                if (int.TryParse(Attr(e, "userAccountControl"), out var uac) && (uac & 2) != 0) return AuthResult.Failed(AuthFail.Disabled);
                if (long.TryParse(Attr(e, "accountExpires"), out var ae) && ae > 0 && ae != long.MaxValue)
                {
                    try { if (DateTime.FromFileTimeUtc(ae) < DateTime.UtcNow) return AuthResult.Failed(AuthFail.AccountExpired); } catch (ArgumentOutOfRangeException) { }
                }

                if (password != null)
                {
                    LdapConnection user;
                    try { (user, _) = await ConnectAsync(o, info); }
                    catch (Exception ex) { return AuthResult.Failed(AuthFail.Unavailable, Explain(ex, info)); }
                    using (user)
                    {
                        try { await user.BindAsync(e.Dn, password); }
                        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
                        { var f = MapBindError(ex); return AuthResult.Failed(f); }
                    }
                }

                var attrs = e.GetAttributeSet();
                var direct = attrs.ContainsKey("memberOf") ? attrs["memberOf"].StringValueArray : Array.Empty<string>();
                var groups = await GroupNamesAsync(svc, direct, o.Nested);
                var roles = new List<string>();
                // a role can be given by several groups: "Group-A, Group-B"
                void Map(string g, string role) { if (MailService.Split(g).Any(groups.Contains)) roles.Add(role); }
                Map(o.GroupStaff, Roles.Staff); Map(o.GroupHod, Roles.Hod); Map(o.GroupMedia, Roles.Media); Map(o.GroupAdmin, Roles.Admin); Map(o.GroupResearch, Roles.Research);
                var sam = Attr(e, "sAMAccountName") ?? login;
                return AuthResult.Ok(new AdUser(sam, Attr(e, "displayName") ?? sam, Attr(e, "mail"), Attr(e, "department"), roles, groups.OrderBy(x => x).ToList()));
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning("LDAP lookup failed for {U}: {M}", login, ex.Message);
            return AuthResult.Failed(AuthFail.Unavailable, Explain(ex, info));
        }
    }

    // ------------------------------------------------------------------ helpers
    /// <summary>DOMAIN\user -> sAMAccountName; user@domain or an email -> userPrincipalName / mail; otherwise sAMAccountName. Computer accounts are excluded.</summary>
    private static string BuildFilter(string login)
    {
        const string person = "(objectClass=user)(!(objectClass=computer))";
        if (login.Contains('\\')) return $"(&{person}(sAMAccountName={Esc(login[(login.IndexOf('\\') + 1)..])}))";
        if (login.Contains('@')) return $"(&{person}(|(userPrincipalName={Esc(login)})(mail={Esc(login)})))";
        return $"(&{person}(sAMAccountName={Esc(login)}))";
    }

    /// <summary>All groups of the user. With <paramref name="nested"/> also the groups those groups belong to (any depth, loops are safe).</summary>
    private static async Task<HashSet<string>> GroupNamesAsync(LdapConnection svc, string[] direct, bool nested)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(direct);
        int lookups = 0;
        while (queue.Count > 0)
        {
            var dn = queue.Dequeue();
            if (!seen.Add(dn)) continue;
            names.Add(FirstRdnValue(dn));
            if (!nested || lookups++ >= 300) continue;
            try
            {
                var r = await svc.SearchAsync(dn, LdapConnection.ScopeBase, "(objectClass=*)", new[] { "memberOf" }, false);
                while (await r.HasMoreAsync())
                {
                    var g = await r.NextAsync(); var a = g.GetAttributeSet();
                    if (a.ContainsKey("memberOf")) foreach (var parent in a["memberOf"].StringValueArray) if (!seen.Contains(parent)) queue.Enqueue(parent);
                }
            }
            catch (LdapException) { } // a group we cannot read is simply not expanded
        }
        return names;
    }

    /// <summary>"CN=Events\, Staff,OU=Groups,DC=x" -> "Events, Staff" (handles escaped commas).</summary>
    internal static string FirstRdnValue(string dn)
    {
        var sb = new System.Text.StringBuilder(); bool afterEq = false;
        for (int i = 0; i < dn.Length; i++)
        {
            var c = dn[i];
            if (c == '\\' && i + 1 < dn.Length) { if (afterEq) sb.Append(dn[i + 1]); i++; continue; }
            if (c == ',' ) break;
            if (!afterEq) { if (c == '=') afterEq = true; continue; }
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    private static AuthFail MapBindError(LdapException ex)
    {
        // Active Directory puts the reason in the message: "... AcceptSecurityContext error, data 775, v4563"
        var m = (ex.LdapErrorMessage ?? "") + " " + ex.Message;
        if (m.Contains("data 775")) return AuthFail.Locked;
        if (m.Contains("data 533")) return AuthFail.Disabled;
        if (m.Contains("data 532") || m.Contains("data 773")) return AuthFail.PasswordExpired;
        if (m.Contains("data 701")) return AuthFail.AccountExpired;
        return AuthFail.Invalid;
    }

    /// <summary>Tries every server in the list (comma separated, optional :port each) until one answers; each gets 6 seconds.</summary>
    private static async Task<(LdapConnection con, string server)> ConnectAsync(LdapOpts o, CertInfo info)
    {
        Exception? last = null;
        var hosts = o.Host.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var raw in hosts)
        {
            string host = raw; int port = o.Port;
            int i = raw.LastIndexOf(':');
            if (i > 0 && !raw.Contains('[') && int.TryParse(raw[(i + 1)..], out var p)) { host = raw[..i]; port = p; }
            var con = new LdapConnection { SecureSocketLayer = o.UseSsl };
            if (o.UseSsl || o.StartTls) con.UserDefinedServerCertValidationDelegate += Validator(o, info);
            try
            {
                var t = con.ConnectAsync(host, port);
                if (await Task.WhenAny(t, Task.Delay(TimeSpan.FromSeconds(6))) != t) throw new TimeoutException($"{host}:{port} did not answer within 6 seconds");
                await t;
                if (o.StartTls && !o.UseSsl) await con.StartTlsAsync();
                return (con, $"{host}:{port}");
            }
            catch (Exception ex) { last = ex; try { con.Dispose(); } catch { } }
        }
        throw last ?? new InvalidOperationException("No LDAP server is configured.");
    }

    /// <summary>Certificate rule: pinned thumbprint wins; else "skip" accepts anything (testing); else the normal Windows trust check.</summary>
    private static Novell.Directory.Ldap.RemoteCertificateValidationCallback Validator(LdapOpts o, CertInfo info) => (_, cert, _, errors) =>
    {
        if (cert != null) info.Thumbprint = new X509Certificate2(cert).Thumbprint;
        if (o.SkipCert) return true;
        var pin = (o.CertThumbprint ?? "").Replace(":", "").Replace(" ", "").Trim();
        if (pin.Length > 0) return string.Equals(info.Thumbprint, pin, StringComparison.OrdinalIgnoreCase);
        return errors == SslPolicyErrors.None;
    };

    /// <summary>Turns low-level exceptions into a sentence an administrator can act on.</summary>
    private static string Explain(Exception ex, CertInfo info)
    {
        var hint = info.Thumbprint != null ? $" Server certificate thumbprint: {info.Thumbprint} - if you trust this server, choose 'Trust this certificate only' and paste it." : "";
        switch (ex)
        {
            case LdapException le when le.ResultCode == LdapException.InvalidCredentials:
                return "The service account was rejected (wrong DN or password).";
            case LdapException le when le.ResultCode == LdapException.NoSuchObject:
                return "The Base DN was not found on this server - check it (for example DC=company,DC=local).";
            case LdapException le when le.ResultCode == LdapException.ConnectError || le.ResultCode == LdapException.ServerDown:
                return "Cannot reach the server, or the secure (TLS) handshake failed - check the host name, the port and the Encryption setting." + hint;
            case LdapException le when le.ResultCode == 4:
                return "The search returned too many entries - use a narrower Base DN.";
            case LdapException le:
                return $"The directory refused the request: {le.Message}";
            case TimeoutException te:
                return $"Cannot reach the server: {te.Message}.";
            case System.Security.Authentication.AuthenticationException:
                return "The secure (TLS) connection failed - the certificate is not trusted or does not match the server name." + hint;
            case System.Net.Sockets.SocketException se:
                return $"Cannot reach the server: {se.Message}";
        }
        var msg = ex.InnerException?.Message ?? ex.Message;
        if (msg.Contains("SSL", StringComparison.OrdinalIgnoreCase) || msg.Contains("TLS", StringComparison.OrdinalIgnoreCase) || msg.Contains("certificate", StringComparison.OrdinalIgnoreCase))
            return $"The secure (TLS) connection failed: {msg}.{hint}";
        return $"Cannot reach the directory: {msg}.{hint}";
    }

    private static string? Attr(LdapEntry e, string n) { var a = e.GetAttributeSet(); return a.ContainsKey(n) ? a[n].StringValue : null; }
    private static string Esc(string s) => s.Replace("\\", "\\5c").Replace("*", "\\2a").Replace("(", "\\28").Replace(")", "\\29").Replace("\0", "\\00");
}

/// <summary>Development-only fake directory. Enabled with Auth:Mode=Dev. Password for all users: "dev".</summary>
public class DevAuthService : IAuthService
{
    private static readonly Dictionary<string, AdUser> Users = new(StringComparer.OrdinalIgnoreCase)
    {
        ["staff"] = new("staff", "Sara Staff", "staff@example.com", "Marketing", new() { Roles.Staff }),
        ["hod"] = new("hod", "Hamad HOD", "hod@example.com", "Management", new() { Roles.Hod }),
        ["media"] = new("media", "Mona Media", "media@example.com", "Media", new() { Roles.Media }),
        ["research"] = new("research", "Rana Research", "research@example.com", "Research", new() { Roles.Research }),
        ["admin"] = new("admin", "Adam Admin", "admin@example.com", "IT", new() { Roles.Admin, Roles.Staff }),
    };
    // dev users sign in by name or by email (staff@example.com), like the real directory
    private static AdUser? Find(string? login) =>
        Users.TryGetValue(login ?? "", out var u) ? u : Users.Values.FirstOrDefault(x => string.Equals(x.Email, login, StringComparison.OrdinalIgnoreCase));
    public Task<AuthResult> AuthenticateAsync(string username, string password) =>
        Task.FromResult(password == "dev" && Find(username) is { } u ? AuthResult.Ok(u) : AuthResult.Failed(AuthFail.Invalid));
    public Task<AuthResult> LookupAsync(string login)
    {
        var name = (login ?? "").Contains('\\') ? login![(login.IndexOf('\\') + 1)..] : (login ?? "");
        return Task.FromResult(Users.TryGetValue(name, out var u) ? AuthResult.Ok(u) : AuthResult.Failed(AuthFail.Invalid));
    }
}
