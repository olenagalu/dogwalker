using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PawsAndPaths.Api.Services;

public interface IAccountDecisionEmailSender
{
    Task SendDeclinedAsync(string recipientEmail, string recipientName,
        CancellationToken cancellationToken = default);
}

public sealed class AccountDecisionEmailSender(
    IConfiguration configuration,
    ILogger<AccountDecisionEmailSender> logger) : IAccountDecisionEmailSender
{
    public async Task SendDeclinedAsync(string recipientEmail, string recipientName,
        CancellationToken cancellationToken = default)
    {
        var host = configuration["Email:SmtpHost"];
        var username = configuration["Email:SmtpUsername"];
        var password = configuration["Email:SmtpPassword"];
        var fromAddress = configuration["Email:FromAddress"] ?? username;
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fromAddress))
        {
            logger.LogWarning("Account-decision email skipped because SMTP is not configured.");
            return;
        }

        var message = AccountDecisionEmailContent.CreateDeclined(
            recipientEmail, recipientName, fromAddress,
            configuration["Email:FromName"] ?? "Princess Dog Walker");
        using var client = new SmtpClient { Timeout = 10_000 };
        await client.ConnectAsync(host, configuration.GetValue("Email:SmtpPort", 587),
            SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(username, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

public static class AccountDecisionEmailContent
{
    public static MimeMessage CreateDeclined(string recipientEmail, string recipientName,
        string fromAddress, string fromName)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = "About your Princess Dog Walker account";
        message.Body = new BodyBuilder
        {
            TextBody = $"Hi {recipientName},\n\nThank you so much for reaching out to Princess Dog Walker. Julia is genuinely sorry, but unfortunately she is not able to take care of your pet at this time. This may be because your location is outside the current service area or because the requested care is not a suitable match.\n\nThank you for understanding. Julia wishes you and your pet all the very best.\n\nWarmly,\nJulia\nPrincess Dog Walker\n561-788-3531",
            HtmlBody = $$"""
                <!doctype html>
                <html lang="en"><body style="margin:0;background:#fff7fb;font-family:Arial,sans-serif;color:#43283a">
                  <div style="max-width:600px;margin:0 auto;padding:32px 24px"><div style="background:#fff;border:1px solid #f3cfdf;border-radius:20px;padding:32px">
                    <p style="margin:0 0 8px;color:#b23a72;font-weight:700">PRINCESS DOG WALKER</p>
                    <h1 style="margin:0 0 20px;font-size:26px;color:#7c2852">Hi {{safeName}},</h1>
                    <p style="font-size:16px;line-height:1.6">Thank you so much for reaching out to Princess Dog Walker.</p>
                    <p style="font-size:16px;line-height:1.6">Julia is genuinely sorry, but unfortunately she is not able to take care of your pet at this time. This may be because your location is outside the current service area or because the requested care is not a suitable match.</p>
                    <p style="font-size:16px;line-height:1.6">Thank you for understanding. Julia wishes you and your pet all the very best.</p>
                    <p style="margin:28px 0 0;line-height:1.6">Warmly,<br><strong>Julia</strong><br>Princess Dog Walker<br><a href="tel:+15617883531" style="color:#b23a72">561-788-3531</a></p>
                  </div></div>
                </body></html>
                """
        }.ToMessageBody();
        return message;
    }
}
