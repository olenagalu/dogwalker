using System.Globalization;
using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PawsAndPaths.Api.Services;

public record BookingDeclinedNotification(
    string CustomerEmail,
    string CustomerName,
    string DogName,
    string ServiceName,
    DateOnly Date);

public interface IBookingDecisionEmailSender
{
    Task SendDeclinedAsync(BookingDeclinedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed class BookingDecisionEmailSender(
    IConfiguration configuration,
    ILogger<BookingDecisionEmailSender> logger) : IBookingDecisionEmailSender
{
    public async Task SendDeclinedAsync(
        BookingDeclinedNotification notification, CancellationToken cancellationToken = default)
    {
        var host = configuration["Email:SmtpHost"];
        var username = configuration["Email:SmtpUsername"];
        var password = configuration["Email:SmtpPassword"];
        var fromAddress = configuration["Email:FromAddress"] ?? username;
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fromAddress))
        {
            logger.LogWarning("Booking-decision email skipped because SMTP is not configured.");
            return;
        }

        var message = BookingDecisionEmailContent.CreateDeclined(
            notification,
            fromAddress,
            configuration["Email:FromName"] ?? "Princess Dog Walker",
            configuration["Owner:Email"] ?? "kadulinaiulia@gmail.com",
            configuration["Owner:Phone"] ?? "561-788-3531");
        using var client = new SmtpClient { Timeout = 10_000 };
        await client.ConnectAsync(host, configuration.GetValue("Email:SmtpPort", 587),
            SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(username, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

public static class BookingDecisionEmailContent
{
    public static MimeMessage CreateDeclined(
        BookingDeclinedNotification notification,
        string fromAddress,
        string fromName,
        string contactEmail,
        string contactPhone)
    {
        var safeName = WebUtility.HtmlEncode(notification.CustomerName);
        var safeDog = WebUtility.HtmlEncode(notification.DogName);
        var safeService = WebUtility.HtmlEncode(notification.ServiceName);
        var safeContactEmail = WebUtility.HtmlEncode(contactEmail);
        var safeContactPhone = WebUtility.HtmlEncode(contactPhone);
        var date = notification.Date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(notification.CustomerEmail));
        message.Subject = "About your Princess Dog Walker booking request";
        message.Body = new BodyBuilder
        {
            TextBody = $"Hi {notification.CustomerName},\n\nThank you for requesting {notification.ServiceName} for {notification.DogName} on {date}. We’re sorry, but Princess Dog Walker does not currently provide service in your area, so we’re unable to approve this booking request.\n\nPlease do not reply to this automated email. If you have any questions, contact Julia directly at {contactEmail} or {contactPhone}.\n\nThank you for understanding,\nJulia\nPrincess Dog Walker",
            HtmlBody = $$"""
                <!doctype html>
                <html lang="en"><body style="margin:0;background:#fff7fb;font-family:Arial,sans-serif;color:#43283a">
                  <div style="max-width:600px;margin:0 auto;padding:32px 24px"><div style="background:#fff;border:1px solid #f3cfdf;border-radius:20px;padding:32px">
                    <p style="margin:0 0 8px;color:#b23a72;font-weight:700">PRINCESS DOG WALKER</p>
                    <h1 style="margin:0 0 20px;font-size:26px;color:#7c2852">Hi {{safeName}},</h1>
                    <p style="font-size:16px;line-height:1.6">Thank you for requesting <strong>{{safeService}}</strong> for {{safeDog}} on {{date}}.</p>
                    <p style="font-size:16px;line-height:1.6">We’re sorry, but Princess Dog Walker does not currently provide service in your area, so we’re unable to approve this booking request.</p>
                    <p style="font-size:16px;line-height:1.6"><strong>Please do not reply to this automated email.</strong> If you have any questions, contact Julia directly at <a href="mailto:{{safeContactEmail}}" style="color:#b23a72">{{safeContactEmail}}</a> or <a href="tel:+1{{safeContactPhone.Replace("-", string.Empty)}}" style="color:#b23a72">{{safeContactPhone}}</a>.</p>
                    <p style="margin:28px 0 0;line-height:1.6">Thank you for understanding,<br><strong>Julia</strong><br>Princess Dog Walker</p>
                  </div></div>
                </body></html>
                """
        }.ToMessageBody();
        return message;
    }
}
