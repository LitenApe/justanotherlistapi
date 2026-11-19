using Api.Sql;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Sql;

public sealed class SqlMembershipTests
{
    [Fact]
    public async Task IsMember_TellsMembersFromOthers()
    {
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId);
        var membership = new SqlMembership(db);
        CancellationToken ct = TestContext.Current.CancellationToken;

        Assert.True(await membership.IsMember(itemGroupId, memberId, ct));
        Assert.False(await membership.IsMember(itemGroupId, Guid.NewGuid(), ct));
        Assert.False(await membership.IsMember(Guid.NewGuid(), memberId, ct));
    }

    [Fact]
    public async Task ListMembers_ReturnsOnlyThatListsMembers()
    {
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId);
        await db.InsertItemGroup(Guid.NewGuid(), Guid.NewGuid());

        IReadOnlyList<Guid> members = await new SqlMembership(db).ListMembers(
            itemGroupId,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(memberId, Assert.Single(members));
    }

    [Fact]
    public async Task Add_AddsTheMember_OnlyOnce()
    {
        var itemGroupId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, Guid.NewGuid());
        var membership = new SqlMembership(db);
        CancellationToken ct = TestContext.Current.CancellationToken;

        Assert.True(await membership.Add(itemGroupId, newMemberId, ct));
        Assert.False(await membership.Add(itemGroupId, newMemberId, ct));
        Assert.Equal(2, (await db.LoadMembers(itemGroupId)).Count);
    }

    [Fact]
    public async Task RemoveUnlessLast_RemovesTheMember_WhenAnotherMemberRemains()
    {
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId, otherMemberId);

        bool removed = await new SqlMembership(db).RemoveUnlessLast(
            itemGroupId,
            memberId,
            TestContext.Current.CancellationToken
        );

        Assert.True(removed);
        Assert.Equal(otherMemberId, Assert.Single(await db.LoadMembers(itemGroupId)));
    }

    [Fact]
    public async Task RemoveUnlessLast_KeepsTheMember_WhenTheyAreTheLastOne()
    {
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId);

        bool removed = await new SqlMembership(db).RemoveUnlessLast(
            itemGroupId,
            memberId,
            TestContext.Current.CancellationToken
        );

        Assert.False(removed);
        Assert.Equal(memberId, Assert.Single(await db.LoadMembers(itemGroupId)));
    }

    [Fact]
    public async Task RemoveUnlessLast_KeepsTheLastMember_EvenIfTheyBelongToAnotherList()
    {
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId);
        await db.InsertItemGroup(Guid.NewGuid(), memberId, Guid.NewGuid());

        bool removed = await new SqlMembership(db).RemoveUnlessLast(
            itemGroupId,
            memberId,
            TestContext.Current.CancellationToken
        );

        Assert.False(removed);
        Assert.Equal(memberId, Assert.Single(await db.LoadMembers(itemGroupId)));
    }

    [Fact]
    public async Task RemoveUnlessLast_ChangesNothing_WhenTheTargetIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId);

        bool removed = await new SqlMembership(db).RemoveUnlessLast(
            itemGroupId,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        Assert.True(removed);
        Assert.Equal(memberId, Assert.Single(await db.LoadMembers(itemGroupId)));
    }
}
