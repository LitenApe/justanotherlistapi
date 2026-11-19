using Api.Checklist;
using Dapper;

namespace Api.Tests.Checklist;

public sealed class ChecklistConnectionExtensionsTests
{
    // IsMember

    [Fact]
    public async Task IsMember_ReturnsFalse_WhenUserIdIsNull()
    {
        await using var db = await TestDatabase.CreateAsync();

        bool result = await db.IsMember(
            Guid.NewGuid(),
            null,
            TestContext.Current.CancellationToken
        );

        Assert.False(result);
    }

    [Fact]
    public async Task IsMember_ReturnsTrue_WhenUserIsMember()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = userId, ItemGroupId = itemGroupId }
        );

        // Act
        bool result = await db.IsMember(itemGroupId, userId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsMember_ReturnsFalse_WhenUserIsNotMember()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );

        // Act
        bool result = await db.IsMember(itemGroupId, userId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }
}
