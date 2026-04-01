using System.Security.Claims;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;

public class GetItemGroupsTests
{
    [Fact]
    public async Task Execute_ReturnsAllItemGroups_ForAuthenticatedUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var group1Id = Guid.NewGuid();
        var group2Id = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = group1Id, Name = "Group 1" });
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = group2Id, Name = "Group 2" });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = group1Id });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = group2Id });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = otherUserId, ItemGroupId = group2Id });
        // Add items (one complete, one incomplete) to group1
        await db.ExecuteAsync("INSERT INTO Items (Id, Name, IsComplete, ItemGroupId) VALUES (@Id, @Name, @IsComplete, @ItemGroupId)",
            new { Id = Guid.NewGuid(), Name = "Incomplete", IsComplete = false, ItemGroupId = group1Id });
        await db.ExecuteAsync("INSERT INTO Items (Id, Name, IsComplete, ItemGroupId) VALUES (@Id, @Name, @IsComplete, @ItemGroupId)",
            new { Id = Guid.NewGuid(), Name = "Complete", IsComplete = true, ItemGroupId = group1Id });

        // Act
        var result = await GetItemGroups.Execute(claimsPrincipal, db, default);

        // Assert
        var ok = Assert.IsType<Ok<List<ItemGroup>>>(result.Result);
        var groups = ok.Value;
        Assert.NotNull(groups);
        Assert.Equal(2, groups.Count);
        var group1 = groups.FirstOrDefault(g => g.Id == group1Id);
        Assert.NotNull(group1);
        // Only incomplete items should be included
        Assert.Single(group1.Items);
        Assert.Equal("Incomplete", group1.Items.First().Name);
    }

    [Fact]
    public async Task Execute_ReturnsEmptyList_WhenUserHasNoGroups()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        // No groups or members for this user

        // Act
        var result = await GetItemGroups.Execute(claimsPrincipal, db, default);

        // Assert
        var ok = Assert.IsType<Ok<List<ItemGroup>>>(result.Result);
        Assert.NotNull(ok.Value);
        Assert.Empty(ok.Value);
    }

    [Fact]
    public async Task Execute_ReturnsUnauthorized_WhenUserIsNull()
    {
        // Arrange
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        await using var db = await TestDatabase.CreateAsync();

        // Act
        var result = await GetItemGroups.Execute(claimsPrincipal, db, default);

        // Assert
        Assert.IsType<UnauthorizedHttpResult>(result.Result);
    }

    [Fact]
    public async Task Execute_ReturnsEmptyMembersForEachGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        var claimsPrincipal = TestHelpers.CreatePrincipal(userId);

        await using var db = await TestDatabase.CreateAsync();
        await db.ExecuteAsync("INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)", new { Id = groupId, Name = "Group" });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = userId, ItemGroupId = groupId });
        await db.ExecuteAsync("INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)", new { MemberId = otherMemberId, ItemGroupId = groupId });

        // Act
        var result = await GetItemGroups.Execute(claimsPrincipal, db, default);

        // Assert — the list endpoint intentionally does not load members
        var ok = Assert.IsType<Ok<List<ItemGroup>>>(result.Result);
        var group = Assert.Single(ok.Value!);
        Assert.Empty(group.Members);
    }
}

