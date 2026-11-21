using Checklist.Domain;

namespace Api.Tests.Checklist.DomainTests;

public sealed class PasswordTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("elevenchars")]
    public void TryCreate_RejectsPasswordsShorterThanTheMinimum(string? value)
    {
        Assert.False(Password.TryCreate(value, out Password? password));
        Assert.Null(password);
    }

    [Fact]
    public void TryCreate_AcceptsAnyCharacters_FromTheMinimumToTheMaximumLength()
    {
        Assert.True(Password.TryCreate("twelve chars", out Password? shortest));
        Assert.Equal("twelve chars", shortest.Value);
        Assert.True(Password.TryCreate(new string('p', Password.MaxLength), out _));
        Assert.False(Password.TryCreate(new string('p', Password.MaxLength + 1), out _));
    }

    [Fact]
    public void TryCreate_KeepsSpacesAtEitherEnd()
    {
        Assert.True(Password.TryCreate("  spaced out  ", out Password? password));
        Assert.Equal("  spaced out  ", password.Value);
    }

    [Fact]
    public void ToString_NeverShowsThePassword()
    {
        Assert.True(Password.TryCreate("correct horse battery", out Password? password));
        Assert.DoesNotContain("horse", password.ToString(), StringComparison.Ordinal);
    }
}
