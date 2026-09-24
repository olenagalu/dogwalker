using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class BookingDecisionEmailContentTests
{
    [Fact]
    public void DeclinedEmail_IsPoliteAndExplainsServiceAreaAndContactOptions()
    {
        var message = BookingDecisionEmailContent.CreateDeclined(
            new BookingDeclinedNotification(
                "customer@example.com", "Taylor & <Pup>", "Buddy", "Dog walk",
                new DateOnly(2026, 10, 2)),
            "sender@example.com", "Princess Dog Walker", "julia@example.com", "561-788-3531");

        Assert.Equal("About your Princess Dog Walker booking request", message.Subject);
        Assert.Contains("does not currently provide service in your area", message.TextBody);
        Assert.Contains("Please do not reply to this automated email", message.TextBody);
        Assert.Contains("contact Julia directly", message.TextBody);
        Assert.Contains("julia@example.com", message.TextBody);
        Assert.Contains("Taylor &amp; &lt;Pup&gt;", message.HtmlBody);
        Assert.DoesNotContain("Taylor & <Pup>", message.HtmlBody);
    }
}
