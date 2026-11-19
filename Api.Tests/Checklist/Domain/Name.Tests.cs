using Checklist.Domain;

namespace Api.Tests.Checklist.DomainTests;

public sealed class NameTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void TryCreate_RejectsBlankNames(string? value)
    {
        Assert.False(Name.TryCreate(value, out Name? name));
        Assert.Null(name);
    }

    [Fact]
    public void TryCreate_KeepsTheNameExactlyAsGiven()
    {
        Assert.True(Name.TryCreate(" Milk ", out Name? name));
        Assert.Equal(" Milk ", name.Value);
    }
}
