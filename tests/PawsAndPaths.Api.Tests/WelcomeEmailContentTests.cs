using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class WelcomeEmailContentTests
{
    [Fact]
    public async Task Sender_DoesNotFailRegistrationWhenSmtpIsNotConfigured()
    {
        var configuration = new ConfigurationBuilder().Build();
        var sender = new WelcomeEmailSender(configuration, NullLogger<WelcomeEmailSender>.Instance);

        await sender.SendAsync("customer@example.com", "Sam Taylor");
    }

    [Fact]
    public void Create_IncludesRecipientAndBusinessDetails()
    {
        var message = WelcomeEmailContent.Create(
            "customer@example.com", "Sam Taylor", "kadulinaiulia@gmail.com", "Princess Dog Walker");

        Assert.Equal("Welcome to Princess Dog Walker!", message.Subject);
        Assert.Equal("customer@example.com", message.To.Mailboxes.Single().Address);
        Assert.Contains("Sam Taylor", message.HtmlBody);
        Assert.Contains("561-788-3531", message.TextBody);
        Assert.Contains("request an available service", message.TextBody);
        Assert.DoesNotContain("review your service area", message.TextBody);
    }

    [Fact]
    public void Create_HtmlEncodesCustomerName()
    {
        var message = WelcomeEmailContent.Create(
            "customer@example.com", "<script>alert('hi')</script>",
            "kadulinaiulia@gmail.com", "Princess Dog Walker");

        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
    }
}
