using GuestEvents.Data;
using GuestEvents.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace GuestEvents.Services;

public class MailService
{
    private readonly ConnectionConfig _cfg;
    private readonly AppDb _db;
    private readonly ILogger<MailService> _log;
    public MailService(ConnectionConfig cfg, AppDb db, ILogger<MailService> log) { _cfg = cfg; _db = db; _log = log; }

    /// <summary>Returns true when the message was handed to SMTP (or SMTP is switched off) without an error.</summary>
    public async Task<bool> SendAsync(int? eventId, IEnumerable<string> to, IEnumerable<string>? cc, string subject, string body, string? attachmentPath = null, string? attachmentName = null)
    {
        var toList = to.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (toList.Count == 0) return false;
        string? error = null;
        try
        {
            var o = await _cfg.SmtpAsync();
            if (o.Enabled) await DeliverAsync(o, toList, cc, subject, body, attachmentPath, attachmentName);
            else _log.LogInformation("SMTP disabled. Would send '{S}' to {T}", subject, string.Join(",", toList));
        }
        catch (Exception ex) { error = ex.Message; _log.LogError(ex, "Mail failed"); }
        foreach (var t in toList)
            _db.Notifications.Add(new Notification { EventRequestId = eventId, Recipient = t, Subject = subject, Success = error == null, Error = error });
        await _db.SaveChangesAsync();
        return error == null;
    }

    /// <summary>Admin "Send test email" with the given (possibly unsaved) settings; always really sends.</summary>
    public async Task<(bool ok, string message)> TestAsync(SmtpOpts o, string to)
    {
        try
        {
            await DeliverAsync(o, new[] { to }, null, "Guest Events - SMTP test", "This is a test message from the Guest Events system. SMTP is configured correctly.", null);
            return (true, $"Test email sent to {to}.");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private static async Task DeliverAsync(SmtpOpts o, IEnumerable<string> to, IEnumerable<string>? cc, string subject, string body, string? attachmentPath, string? attachmentName = null)
    {
        var m = new MimeMessage();
        m.From.Add(MailboxAddress.Parse(o.From));
        foreach (var t in to) m.To.Add(MailboxAddress.Parse(t));
        foreach (var c in (cc ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x))) m.Cc.Add(MailboxAddress.Parse(c));
        m.Subject = subject;
        var bb = new BodyBuilder { TextBody = body };
        if (attachmentPath != null && File.Exists(attachmentPath))
        {
            if (string.IsNullOrEmpty(attachmentName)) bb.Attachments.Add(attachmentPath);
            else bb.Attachments.Add(attachmentName, await File.ReadAllBytesAsync(attachmentPath)); // show the original file name, not the stored one
        }
        m.Body = bb.ToMessageBody();
        using var smtp = new SmtpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var sec = o.Port == 465 ? SecureSocketOptions.SslOnConnect : o.StartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await smtp.ConnectAsync(o.Host, o.Port, sec, cts.Token);
        if (!string.IsNullOrEmpty(o.User)) await smtp.AuthenticateAsync(o.User, o.Password, cts.Token);
        await smtp.SendAsync(m, cts.Token);
        await smtp.DisconnectAsync(true, cts.Token);
    }

    public static string Fill(string tpl, Dictionary<string, string> v)
    {
        foreach (var kv in v) tpl = tpl.Replace("{" + kv.Key + "}", kv.Value);
        return tpl;
    }

    public static List<string> Split(string? s) => (s ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
