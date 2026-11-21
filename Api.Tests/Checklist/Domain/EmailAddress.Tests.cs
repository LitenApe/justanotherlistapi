using Checklist.Domain;

namespace Api.Tests.Checklist.DomainTests;

public sealed class EmailAddressTests
{
    [Theory]
    [InlineData("kim@example.com")]
    [InlineData("Kim.Lee+lists@sub.example.org")]
    [InlineData("o'brien@example.ie")]
    [InlineData("a@b")]
    public void TryCreate_AcceptsAddressesWithOneAtAndTextOnBothSides(string value)
    {
        Assert.True(EmailAddress.TryCreate(value, out EmailAddress? email));
        Assert.Equal(value, email.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kim")]
    [InlineData("@example.com")]
    [InlineData("kim@")]
    [InlineData("kim@@example.com")]
    [InlineData("kim@home@example.com")]
    [InlineData("kim lee@example.com")]
    [InlineData("kim@exa\nmple.com")]
    public void TryCreate_RejectsAnythingElse(string? value)
    {
        Assert.False(EmailAddress.TryCreate(value, out EmailAddress? email));
        Assert.Null(email);
    }

    [Fact]
    public void TryCreate_TrimsTheAddress_AndKeepsItsCase()
    {
        Assert.True(EmailAddress.TryCreate("  Kim@Example.com ", out EmailAddress? email));
        Assert.Equal("Kim@Example.com", email.Value);
    }

    [Fact]
    public void TryCreate_AcceptsUpToTheMaximumLength()
    {
        string domain = "@example.com";
        string longest = new string('k', EmailAddress.MaxLength - domain.Length) + domain;

        Assert.True(EmailAddress.TryCreate(longest, out _));
        Assert.False(EmailAddress.TryCreate("k" + longest, out _));
    }
}
