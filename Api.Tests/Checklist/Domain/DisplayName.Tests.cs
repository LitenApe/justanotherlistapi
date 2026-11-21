using Checklist.Domain;

namespace Api.Tests.Checklist.DomainTests;

public sealed class DisplayNameTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void TryCreate_RejectsBlankNames(string? value)
    {
        Assert.False(DisplayName.TryCreate(value, out DisplayName? name));
        Assert.Null(name);
    }

    [Fact]
    public void TryCreate_TrimsTheName()
    {
        Assert.True(DisplayName.TryCreate("  Kim Lee \t", out DisplayName? name));
        Assert.Equal("Kim Lee", name.Value);
    }

    [Fact]
    public void TryCreate_AcceptsUpToTheMaximumLength_AfterTrimming()
    {
        string longest = new('a', DisplayName.MaxLength);

        Assert.True(DisplayName.TryCreate($" {longest} ", out DisplayName? name));
        Assert.Equal(longest, name.Value);
        Assert.False(DisplayName.TryCreate(longest + "a", out _));
    }
}
