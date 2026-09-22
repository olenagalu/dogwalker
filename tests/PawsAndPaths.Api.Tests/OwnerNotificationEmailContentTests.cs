using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class OwnerNotificationEmailContentTests
{
    [Fact]
    public async Task Sender_DoesNotFailWhenSmtpIsNotConfigured()
    {
        var configuration = new ConfigurationBuilder().Build();
        var sender = new OwnerNotificationEmailSender(
            configuration, NullLogger<OwnerNotificationEmailSender>.Instance);

        await sender.SendContactMessageAsync(
            new ContactNotification("Sam Taylor", "customer@example.com", "Can Julia help?"));
    }

    [Fact]
    public void ContactMessage_IsSentToOwnerWithCustomerReplyTo()
    {
        var message = OwnerNotificationEmailContent.CreateContactMessage(
            new ContactNotification("Sam Taylor", "customer@example.com", "Can Julia help?"),
            "kadulinaiulia@gmail.com", "sender@example.com", "Princess Dog Walker");

        Assert.Equal("kadulinaiulia@gmail.com", message.To.Mailboxes.Single().Address);
        Assert.Equal("customer@example.com", message.ReplyTo.Mailboxes.Single().Address);
        Assert.Contains("Sam Taylor", message.Subject);
        Assert.Contains("Can Julia help?", message.TextBody);
    }

    [Fact]
    public void BookingRequest_IncludesBookingAndCustomerDetails()
    {
        var message = OwnerNotificationEmailContent.CreateBookingRequest(
            new BookingNotification(
                "Sam Taylor", "customer@example.com", "561-555-0100", "Buddy", "Overnight stay",
                new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 4),
                new TimeOnly(22, 0), new TimeOnly(9, 0), 95m, "Needs medication"),
            "kadulinaiulia@gmail.com", "sender@example.com", "Princess Dog Walker",
            "https://princess-dog-walker.onrender.com/owner.html#owner-messages");

        Assert.Equal("kadulinaiulia@gmail.com", message.To.Mailboxes.Single().Address);
        Assert.Equal("customer@example.com", message.ReplyTo.Mailboxes.Single().Address);
        Assert.Contains("Overnight stay", message.Subject);
        Assert.Contains("Buddy", message.TextBody);
        Assert.Contains("October 2, 2026 through October 4, 2026", message.TextBody);
        Assert.Contains("Needs medication", message.TextBody);
        Assert.Contains("owner.html#owner-messages", message.TextBody);
        Assert.Contains("approve or decline", message.HtmlBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HtmlContent_EncodesCustomerProvidedValues()
    {
        var message = OwnerNotificationEmailContent.CreateContactMessage(
            new ContactNotification("<script>alert('hi')</script>", "customer@example.com", "<b>hello</b>"),
            "kadulinaiulia@gmail.com", "sender@example.com", "Princess Dog Walker");

        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.DoesNotContain("<b>hello</b>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
        Assert.Contains("&lt;b&gt;hello&lt;/b&gt;", message.HtmlBody);
    }
}
