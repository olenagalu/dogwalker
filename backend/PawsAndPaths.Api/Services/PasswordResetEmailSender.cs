using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PawsAndPaths.Api.Services;

public interface IPasswordResetEmailSender
{
    Task SendAsync(string recipientEmail, string recipientName, string resetUrl,
        CancellationToken cancellationToken = default);
}

public sealed class PasswordResetEmailSender(IConfiguration configuration, ILogger<PasswordResetEmailSender> logger)
    : IPasswordResetEmailSender
{
    public async Task SendAsync(string recipientEmail, string recipientName, string resetUrl,
        CancellationToken cancellationToken = default)
    {
        var host = configuration["Email:SmtpHost"];
        var username = configuration["Email:SmtpUsername"];
        var password = configuration["Email:SmtpPassword"];
        var from = configuration["Email:FromAddress"] ?? username;
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(from))
        {
            logger.LogWarning("Password reset email skipped because SMTP is not configured.");
            return;
        }

        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeUrl = WebUtility.HtmlEncode(resetUrl);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(configuration["Email:FromName"] ?? "Princess Dog Walker", from));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = "Reset your Princess Dog Walker password";
        message.Body = new BodyBuilder
        {
            TextBody = $"Hi {recipientName},\n\nReset your password: {resetUrl}\n\nIf you did not request this, you can ignore this email.",
            HtmlBody = $"<p>Hi {safeName},</p><p><a href=\"{safeUrl}\">Reset your password</a></p><p>If you did not request this, you can ignore this email.</p>"
        }.ToMessageBody();
        using var client = new SmtpClient { Timeout = 10_000 };
        await client.ConnectAsync(host, configuration.GetValue("Email:SmtpPort", 587), SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(username, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
