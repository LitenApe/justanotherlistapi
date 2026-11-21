using System.Net.Mail;
using Api.Email;
using Checklist.Domain;

namespace Api.Tests.Accounts;

public sealed class SmtpMailerTests
{
    private static readonly User kim = new()
    {
        Id = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
        Name = "Kim Lee",
        Email = "kim@example.com",
    };

    [Fact]
    public void PasswordResetMessage_GoesToTheAccount_WithALinkToTheClientsResetPage()
    {
        var settings = new MailOptions
        {
            From = "Lists <lists@example.com>",
            ClientUrl = "https://lists.example.com/",
        };

        using MailMessage message = SmtpMailer.PasswordResetMessage(settings, kim, "c0de-_x");

        Assert.Equal("kim@example.com", Assert.Single(message.To).Address);
        Assert.Equal("Kim Lee", message.To[0].DisplayName);
        Assert.Equal("lists@example.com", message.From?.Address);
        Assert.False(message.IsBodyHtml);
        Assert.Contains(
            "https://lists.example.com/reset-password?user=a1b2c3d4-e5f6-7890-abcd-ef1234567890&code=c0de-_x",
            message.Body,
            StringComparison.Ordinal
        );
        Assert.Contains("Hi Kim Lee,", message.Body, StringComparison.Ordinal);
    }
}
