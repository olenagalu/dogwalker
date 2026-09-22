using PawsAndPaths.Api.Services;

namespace PawsAndPaths.Api.Tests;

public class AccountDecisionEmailContentTests
{
    [Fact]
    public void DeclinedEmail_IsPoliteAndExplainsPossibleReasons()
    {
        var message = AccountDecisionEmailContent.CreateDeclined(
            "customer@example.com", "Taylor & <Pup>", "julia@example.com", "Princess Dog Walker");

        Assert.Equal("About your Princess Dog Walker account", message.Subject);
        Assert.Contains("genuinely sorry", message.TextBody);
        Assert.Contains("location is outside", message.TextBody);
        Assert.Contains("Taylor &amp; &lt;Pup&gt;", message.HtmlBody);
        Assert.DoesNotContain("Taylor & <Pup>", message.HtmlBody);
    }
}
