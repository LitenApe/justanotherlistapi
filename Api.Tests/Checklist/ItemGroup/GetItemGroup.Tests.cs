using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.ItemGroupTests;

public sealed class GetItemGroupTests
{
    [Fact]
    public async Task Execute_ReturnsTheList_WithAllItemsAndMembers()
    {
        var userId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId, otherMemberId);
        await db.InsertUser(otherMemberId, "Kim");
        await db.InsertItem(itemGroupId, Guid.NewGuid(), "Open");
        await db.InsertItem(itemGroupId, Guid.NewGuid(), "Done", DateTimeOffset.UnixEpoch);

        ItemGroup? itemGroup = await GetUseCase(db)
            .Execute(new Caller(userId), itemGroupId, TestContext.Current.CancellationToken);

        Assert.NotNull(itemGroup);
        Assert.Equal(itemGroupId, itemGroup.Id);
        Assert.Equal("Group", itemGroup.Name);
        Assert.Equal(2, itemGroup.Items.Count);
        Assert.Contains(itemGroup.Items, item =>
            string.Equals(item.Name, "Open", StringComparison.Ordinal)
        );
        Assert.Contains(itemGroup.Items, item =>
            string.Equals(item.Name, "Done", StringComparison.Ordinal)
        );
        // Members come with their names; one without an account has none.
        Assert.Equivalent(
            new[]
            {
                new Member { Id = userId, Name = null },
                new Member { Id = otherMemberId, Name = "Kim" },
            },
            itemGroup.Members,
            strict: true
        );
    }

    [Fact]
    public async Task Execute_ReturnsNull_WhenTheCallerIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, Guid.NewGuid());

        ItemGroup? itemGroup = await GetUseCase(db)
            .Execute(
                new Caller(Guid.NewGuid()),
                itemGroupId,
                TestContext.Current.CancellationToken
            );

        Assert.Null(itemGroup);
    }

    private static GetItemGroup GetUseCase(SqlConnection db) =>
        new(new SqlMembership(db), new SqlItemGroupStore(db));
}
