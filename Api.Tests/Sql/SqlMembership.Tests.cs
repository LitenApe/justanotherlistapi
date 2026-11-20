using Api.Sql;
using Checklist.Domain;
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
    public async Task RemoveUnlessLast_FreesTheItemsReservedForTheMember_AndNothingElse()
    {
        var itemGroupId = Guid.NewGuid();
        var otherGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        var theirOwn = Guid.NewGuid();
        var givenToThem = Guid.NewGuid();
        var givenByThem = Guid.NewGuid();
        var inOtherList = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId, otherMemberId);
        await db.InsertItemGroup(otherGroupId, memberId, otherMemberId);
        await db.InsertItem(itemGroupId, theirOwn, reservedFor: memberId);
        await db.InsertItem(
            itemGroupId,
            givenToThem,
            completedAt: DateTimeOffset.UnixEpoch,
            reservedFor: memberId,
            reservedBy: otherMemberId
        );
        await db.InsertItem(
            itemGroupId,
            givenByThem,
            reservedFor: otherMemberId,
            reservedBy: memberId
        );
        await db.InsertItem(otherGroupId, inOtherList, reservedFor: memberId);

        bool removed = await new SqlMembership(db).RemoveUnlessLast(
            itemGroupId,
            memberId,
            TestContext.Current.CancellationToken
        );

        // Open and done items alike are free again.
        Assert.True(removed);
        foreach (Guid freed in new[] { theirOwn, givenToThem })
        {
            Item? item = await db.LoadItem(freed);
            Assert.NotNull(item);
            Assert.Null(item.ReservedFor);
            Assert.Null(item.ReservedBy);
        }

        // What they reserved for someone else stays reserved for that person.
        Item? kept = await db.LoadItem(givenByThem);
        Assert.Equal(otherMemberId, kept?.ReservedFor);
        Assert.Equal(memberId, kept?.ReservedBy);

        // They're still a member of the other list, so their reservation there stays.
        Assert.Equal(memberId, (await db.LoadItem(inOtherList))?.ReservedFor);
    }

    [Fact]
    public async Task RemoveUnlessLast_KeepsTheirReservations_WhenItRefusesToRemoveTheLastMember()
    {
        var itemGroupId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, memberId);
        await db.InsertItem(itemGroupId, itemId, reservedFor: memberId);

        bool removed = await new SqlMembership(db).RemoveUnlessLast(
            itemGroupId,
            memberId,
            TestContext.Current.CancellationToken
        );

        Assert.False(removed);
        Assert.Equal(memberId, (await db.LoadItem(itemId))?.ReservedFor);
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
