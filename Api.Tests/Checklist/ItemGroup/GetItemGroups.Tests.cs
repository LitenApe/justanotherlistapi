using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.ItemGroupTests;

public sealed class GetItemGroupsTests
{
    [Fact]
    public async Task Execute_ReturnsTheCallersLists_WithTheirOpenItemsOnly()
    {
        var userId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var openItemId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(firstId, userId);
        await db.InsertItemGroup(secondId, userId, Guid.NewGuid());
        await db.InsertItemGroup(Guid.NewGuid(), Guid.NewGuid());
        await db.InsertItem(firstId, openItemId, "Open");
        await db.InsertItem(firstId, Guid.NewGuid(), "Done", DateTimeOffset.UnixEpoch);

        IReadOnlyList<ItemGroup> itemGroups = await new GetItemGroups(
            new SqlItemGroupStore(db)
        ).Execute(new Caller(userId), TestContext.Current.CancellationToken);

        Assert.Equal(2, itemGroups.Count);
        ItemGroup first = Assert.Single(itemGroups, g => g.Id == firstId);
        Assert.Equal(openItemId, Assert.Single(first.Items).Id);
        Assert.Empty(Assert.Single(itemGroups, g => g.Id == secondId).Items);

        // The overview doesn't need the members, so it doesn't load them.
        Assert.All(itemGroups, itemGroup => Assert.Empty(itemGroup.Members));
    }

    [Fact]
    public async Task Execute_ReturnsNothing_WhenTheCallerHasNoLists()
    {
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(Guid.NewGuid(), Guid.NewGuid());

        IReadOnlyList<ItemGroup> itemGroups = await new GetItemGroups(
            new SqlItemGroupStore(db)
        ).Execute(new Caller(Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Empty(itemGroups);
    }
}
