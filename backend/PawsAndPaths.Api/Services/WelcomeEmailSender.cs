using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PawsAndPaths.Api.Services;

public interface IWelcomeEmailSender
{
    Task SendAsync(string recipientEmail, string recipientName, CancellationToken cancellationToken = default);
}

public sealed class WelcomeEmailSender(IConfiguration configuration, ILogger<WelcomeEmailSender> logger)
    : IWelcomeEmailSender
{
    public async Task SendAsync(string recipientEmail, string recipientName,
        CancellationToken cancellationToken = default)
    {
        var host = configuration["Email:SmtpHost"];
        var username = configuration["Email:SmtpUsername"];
        var password = configuration["Email:SmtpPassword"];
        var fromAddress = configuration["Email:FromAddress"] ?? username;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fromAddress))
        {
            logger.LogInformation("Welcome email skipped because SMTP is not configured.");
            return;
        }

        var message = WelcomeEmailContent.Create(recipientEmail, recipientName, fromAddress,
            configuration["Email:FromName"] ?? "Princess Dog Walker");
        var port = configuration.GetValue("Email:SmtpPort", 587);

        using var client = new SmtpClient { Timeout = 10_000 };
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(username, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

public static class WelcomeEmailContent
{
    public static MimeMessage Create(string recipientEmail, string recipientName,
        string fromAddress, string fromName)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = "Welcome to Princess Dog Walker!";
        message.Body = new BodyBuilder
        {
            TextBody = $"Hi {recipientName},\n\nWelcome to Princess Dog Walker! Your account is ready. You can now add your dogs, view availability, and request pet care in Boca Raton.\n\nWith love and happy tails,\nJulia\nPrincess Dog Walker\n561-788-3531",
            HtmlBody = $$"""
                <!doctype html>
                <html lang="en">
                <body style="margin:0;background:#fff7fb;font-family:Arial,sans-serif;color:#43283a">
                  <div style="max-width:600px;margin:0 auto;padding:32px 24px">
                    <div style="background:#ffffff;border:1px solid #f3cfdf;border-radius:20px;padding:32px">
                      <p style="margin:0 0 8px;color:#b23a72;font-weight:700">PRINCESS DOG WALKER ✨</p>
                      <h1 style="margin:0 0 20px;font-size:28px;color:#7c2852">Welcome, {{safeName}}!</h1>
                      <p style="font-size:16px;line-height:1.6">Your account is ready. You can now add your dogs, view Julia’s availability, and request pet care in Boca Raton.</p>
                      <p style="font-size:16px;line-height:1.6">We’re so happy to have you and your pup here. 🐾</p>
                      <p style="margin:28px 0 0;line-height:1.6">With love and happy tails,<br><strong>Julia</strong><br>Princess Dog Walker<br><a href="tel:+15617883531" style="color:#b23a72">561-788-3531</a></p>
                    </div>
                  </div>
                </body>
                </html>
                """
        }.ToMessageBody();
        return message;
    }
}
