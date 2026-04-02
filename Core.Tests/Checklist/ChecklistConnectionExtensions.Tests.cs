using Core.Checklist;
using Dapper;

namespace Core.Tests.Checklist;

public sealed class ChecklistConnectionExtensionsTests
{
    // IsMember

    [Fact]
    public async Task IsMember_ReturnsFalse_WhenUserIdIsNull()
    {
        await using var db = await TestDatabase.CreateAsync();

        bool result = await db.IsMember(Guid.NewGuid(), null);

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
        bool result = await db.IsMember(itemGroupId, userId);

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
        bool result = await db.IsMember(itemGroupId, userId);

        // Assert
        Assert.False(result);
    }

    // IsLastMember

    [Fact]
    public async Task IsLastMember_ReturnsTrue_WhenMemberIsOnlyMember()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = memberId, ItemGroupId = itemGroupId }
        );

        // Act
        bool result = await db.IsLastMember(itemGroupId, memberId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsLastMember_ReturnsFalse_WhenMemberIsOneOfMany()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = memberId, ItemGroupId = itemGroupId }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = Guid.NewGuid(), ItemGroupId = itemGroupId }
        );

        // Act
        bool result = await db.IsLastMember(itemGroupId, memberId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsLastMember_ReturnsFalse_WhenMemberIsNotInGroup()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );
        await db.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = Guid.NewGuid(), ItemGroupId = itemGroupId }
        );

        // Act
        bool result = await db.IsLastMember(itemGroupId, Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsLastMember_ReturnsFalse_WhenGroupHasNoMembers()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Group" }
        );

        // Act
        bool result = await db.IsLastMember(itemGroupId, Guid.NewGuid());

        // Assert
        Assert.False(result);
    }
}
