using System.Net;
using Checklist.Domain;

namespace Api.Tests.Checklist.ItemTests;

public sealed class UnreserveItemHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task MapEndpoint_ReturnsNoContent_AndFreesTheItem()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(
            itemGroupId,
            TestAuthHandler.UserId,
            otherMemberId
        );
        await factory.Connection.InsertItem(itemGroupId, itemId, reservedFor: otherMemberId);

        HttpResponseMessage response = await factory
            .CreateClient()
            .DeleteAsync(
                Path(itemGroupId, itemId, otherMemberId),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Item? stored = await factory.Connection.LoadItem(itemId);
        Assert.Null(stored?.ReservedFor);
        Assert.Null(stored?.ReservedBy);
    }

    // The member the caller saw is required, so a cancellation never frees an item unseen.
    [Fact]
    public async Task MapEndpoint_ReturnsBadRequest_WhenTheMemberIsMissing()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(itemGroupId, TestAuthHandler.UserId);
        await factory.Connection.InsertItem(
            itemGroupId,
            itemId,
            reservedFor: TestAuthHandler.UserId
        );

        HttpResponseMessage response = await factory
            .CreateClient()
            .DeleteAsync(
                $"/api/list/{itemGroupId}/{itemId}/reservation",
                TestContext.Current.CancellationToken
            );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            TestAuthHandler.UserId,
            (await factory.Connection.LoadItem(itemId))?.ReservedFor
        );
    }

    [Fact]
    public async Task MapEndpoint_ReturnsForbidden_WhenTheUserIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(itemGroupId, memberId);
        await factory.Connection.InsertItem(itemGroupId, itemId, reservedFor: memberId);

        HttpResponseMessage response = await factory
            .CreateClient()
            .DeleteAsync(
                Path(itemGroupId, itemId, memberId),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(memberId, (await factory.Connection.LoadItem(itemId))?.ReservedFor);
    }

    [Fact]
    public async Task MapEndpoint_ReturnsConflict_WhenTheListProtectsTheReservation()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(
            itemGroupId,
            TestAuthHandler.UserId,
            otherMemberId
        );
        await factory.Connection.SetItemGroupSettings(
            itemGroupId,
            new ItemGroupSettings { ProtectReservations = true }
        );
        await factory.Connection.InsertItem(itemGroupId, itemId, reservedFor: otherMemberId);

        HttpResponseMessage response = await factory
            .CreateClient()
            .DeleteAsync(
                Path(itemGroupId, itemId, otherMemberId),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(otherMemberId, (await factory.Connection.LoadItem(itemId))?.ReservedFor);
    }

    private static string Path(Guid itemGroupId, Guid itemId, Guid memberId) =>
        $"/api/list/{itemGroupId}/{itemId}/reservation?memberId={memberId}";
}
