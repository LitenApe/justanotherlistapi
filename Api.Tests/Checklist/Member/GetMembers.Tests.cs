using Api.Sql;
using Checklist.Domain;
using Checklist.UseCases;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Api.Tests.Checklist.MemberTests;

public sealed class GetMembersTests
{
    [Fact]
    public async Task Execute_ReturnsEveryMember_WhenTheCallerIsAMember()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId, otherUserId);

        IReadOnlyList<Guid>? members = await new GetMembers(new SqlMembership(db)).Execute(
            new Caller(userId),
            itemGroupId,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(members);
        Assert.Equal(2, members.Count);
        Assert.Contains(userId, members);
        Assert.Contains(otherUserId, members);
    }

    [Fact]
    public async Task Execute_ReturnsNull_WhenTheCallerIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, Guid.NewGuid());

        IReadOnlyList<Guid>? members = await new GetMembers(new SqlMembership(db)).Execute(
            new Caller(Guid.NewGuid()),
            itemGroupId,
            TestContext.Current.CancellationToken
        );

        Assert.Null(members);
    }

    [Fact]
    public async Task Execute_ReturnsNull_WhenTheCallersMembershipWasRevoked()
    {
        var userId = Guid.NewGuid();
        var itemGroupId = Guid.NewGuid();
        await using SqlConnection db = await TestDatabase.CreateAsync();
        await db.InsertItemGroup(itemGroupId, userId);
        await db.ExecuteAsync(
            "DELETE FROM Members WHERE ItemGroupId = @ItemGroupId",
            new { ItemGroupId = itemGroupId }
        );

        IReadOnlyList<Guid>? members = await new GetMembers(new SqlMembership(db)).Execute(
            new Caller(userId),
            itemGroupId,
            TestContext.Current.CancellationToken
        );

        Assert.Null(members);
    }
}
