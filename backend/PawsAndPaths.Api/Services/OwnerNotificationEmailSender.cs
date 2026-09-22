using System.Globalization;
using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PawsAndPaths.Api.Services;

public record ContactNotification(string Name, string Email, string Message);

public record BookingNotification(
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string DogName,
    string ServiceName,
    DateOnly Date,
    DateOnly? EndDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal Price,
    string SpecialInstructions);

public interface IOwnerNotificationEmailSender
{
    Task SendContactMessageAsync(ContactNotification notification, CancellationToken cancellationToken = default);
    Task SendBookingRequestAsync(BookingNotification notification, CancellationToken cancellationToken = default);
}

public sealed class OwnerNotificationEmailSender(
    IConfiguration configuration,
    ILogger<OwnerNotificationEmailSender> logger) : IOwnerNotificationEmailSender
{
    public Task SendContactMessageAsync(
        ContactNotification notification, CancellationToken cancellationToken = default) =>
        SendAsync(OwnerNotificationEmailContent.CreateContactMessage(
            notification,
            OwnerEmail,
            FromAddress,
            configuration["Email:FromName"] ?? "Princess Dog Walker"), cancellationToken);

    public Task SendBookingRequestAsync(
        BookingNotification notification, CancellationToken cancellationToken = default) =>
        SendAsync(OwnerNotificationEmailContent.CreateBookingRequest(
            notification,
            OwnerEmail,
            FromAddress,
            configuration["Email:FromName"] ?? "Princess Dog Walker",
            $"{(configuration["PublicBaseUrl"] ?? "https://princess-dog-walker.onrender.com").TrimEnd('/')}/owner.html#owner-messages"), cancellationToken);

    private string OwnerEmail => configuration["Owner:Email"] ?? "kadulinaiulia@gmail.com";
    private string FromAddress => configuration["Email:FromAddress"]
        ?? configuration["Email:SmtpUsername"]
        ?? OwnerEmail;

    private async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var host = configuration["Email:SmtpHost"];
        var username = configuration["Email:SmtpUsername"];
        var password = configuration["Email:SmtpPassword"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Owner notification email skipped because SMTP is not configured.");
            return;
        }

        var port = configuration.GetValue("Email:SmtpPort", 587);
        using var client = new SmtpClient { Timeout = 10_000 };
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(username, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

public static class OwnerNotificationEmailContent
{
    public static MimeMessage CreateContactMessage(
        ContactNotification notification, string ownerEmail, string fromAddress, string fromName)
    {
        var safeName = WebUtility.HtmlEncode(notification.Name);
        var safeEmail = WebUtility.HtmlEncode(notification.Email);
        var safeMessage = WebUtility.HtmlEncode(notification.Message).Replace("\n", "<br>");
        var message = CreateBase(ownerEmail, fromAddress, fromName, notification.Email,
            $"New website message from {notification.Name}");
        message.Body = new BodyBuilder
        {
            TextBody = $"New message from the Princess Dog Walker website\n\nName: {notification.Name}\nEmail: {notification.Email}\n\nMessage:\n{notification.Message}",
            HtmlBody = $$"""
                <h1>New website message</h1>
                <p><strong>From:</strong> {{safeName}}<br><strong>Email:</strong> {{safeEmail}}</p>
                <p><strong>Message:</strong><br>{{safeMessage}}</p>
                """
        }.ToMessageBody();
        return message;
    }

    public static MimeMessage CreateBookingRequest(
        BookingNotification notification, string ownerEmail, string fromAddress, string fromName,
        string? ownerDashboardUrl = null)
    {
        var dateText = notification.EndDate is null
            ? notification.Date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)
            : $"{notification.Date:MMMM d, yyyy} through {notification.EndDate:MMMM d, yyyy}";
        var timeText = notification.EndDate is null
            ? $"{notification.StartTime:h:mm tt}–{notification.EndTime:h:mm tt}"
            : $"Check-in {notification.StartTime:h:mm tt}; checkout {notification.EndTime:h:mm tt}";
        var notes = string.IsNullOrWhiteSpace(notification.SpecialInstructions)
            ? "None provided"
            : notification.SpecialInstructions;
        var safe = new
        {
            Customer = WebUtility.HtmlEncode(notification.CustomerName),
            Email = WebUtility.HtmlEncode(notification.CustomerEmail),
            Phone = WebUtility.HtmlEncode(notification.CustomerPhone),
            Dog = WebUtility.HtmlEncode(notification.DogName),
            Service = WebUtility.HtmlEncode(notification.ServiceName),
            Date = WebUtility.HtmlEncode(dateText),
            Time = WebUtility.HtmlEncode(timeText),
            Notes = WebUtility.HtmlEncode(notes).Replace("\n", "<br>")
        };
        var message = CreateBase(ownerEmail, fromAddress, fromName, notification.CustomerEmail,
            $"New booking request: {notification.ServiceName} for {notification.DogName}");
        var dashboardText = string.IsNullOrWhiteSpace(ownerDashboardUrl)
            ? string.Empty
            : $"\n\nApprove or decline this request: {ownerDashboardUrl}";
        var dashboardHtml = string.IsNullOrWhiteSpace(ownerDashboardUrl)
            ? string.Empty
            : $"<p><a href=\"{WebUtility.HtmlEncode(ownerDashboardUrl)}\">Open the owner dashboard to approve or decline</a></p>";
        message.Body = new BodyBuilder
        {
            TextBody = $"New booking request\n\nCustomer: {notification.CustomerName}\nEmail: {notification.CustomerEmail}\nPhone: {notification.CustomerPhone}\nDog: {notification.DogName}\nService: {notification.ServiceName}\nDate: {dateText}\nTime: {timeText}\nPrice: {notification.Price:C}\nStatus: Pending\nSpecial instructions: {notes}{dashboardText}",
            HtmlBody = $$"""
                <h1>New booking request</h1>
                <p><strong>Customer:</strong> {{safe.Customer}}<br>
                <strong>Email:</strong> {{safe.Email}}<br>
                <strong>Phone:</strong> {{safe.Phone}}</p>
                <p><strong>Dog:</strong> {{safe.Dog}}<br>
                <strong>Service:</strong> {{safe.Service}}<br>
                <strong>Date:</strong> {{safe.Date}}<br>
                <strong>Time:</strong> {{safe.Time}}<br>
                <strong>Price:</strong> {{notification.Price:C}}<br>
                <strong>Status:</strong> Pending</p>
                <p><strong>Special instructions:</strong><br>{{safe.Notes}}</p>
                {{dashboardHtml}}
                """
        }.ToMessageBody();
        return message;
    }

    private static MimeMessage CreateBase(
        string ownerEmail, string fromAddress, string fromName, string replyTo, string subject)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(ownerEmail));
        message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        message.Subject = subject;
        return message;
    }
}
