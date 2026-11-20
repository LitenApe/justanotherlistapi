using System.Net;
using System.Text;
using Checklist.Domain;

namespace Api.Tests.Checklist.ItemTests;

public sealed class ReserveItemHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task MapEndpoint_ReturnsNoContent_AndReservesTheItem()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(
            itemGroupId,
            TestAuthHandler.UserId,
            otherMemberId
        );
        await factory.Connection.InsertItem(itemGroupId, itemId, "Tent");

        HttpResponseMessage response = await factory
            .CreateClient()
            .PutAsJsonAsync(
                Path(itemGroupId, itemId),
                new { MemberId = otherMemberId, Replacing = (Guid?)null },
                TestContext.Current.CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Item? stored = await factory.Connection.LoadItem(itemId);
        Assert.Equal(otherMemberId, stored?.ReservedFor);
        Assert.Equal(TestAuthHandler.UserId, stored?.ReservedBy);
    }

    // Both fields are required, so a client can't replace a reservation by leaving one out.
    [Theory]
    [InlineData("""{ "memberId": "00000000-0000-0000-0000-000000000001" }""")]
    [InlineData("""{ "replacing": null }""")]
    public async Task MapEndpoint_ReturnsBadRequest_WhenAFieldIsMissing(string body)
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(itemGroupId, TestAuthHandler.UserId);
        await factory.Connection.InsertItem(itemGroupId, itemId);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await factory
            .CreateClient()
            .PutAsync(Path(itemGroupId, itemId), content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null((await factory.Connection.LoadItem(itemId))?.ReservedFor);
    }

    [Fact]
    public async Task MapEndpoint_ReturnsForbidden_WhenTheUserIsNotAMember()
    {
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(itemGroupId, memberId);
        await factory.Connection.InsertItem(itemGroupId, itemId);

        HttpResponseMessage response = await factory
            .CreateClient()
            .PutAsJsonAsync(
                Path(itemGroupId, itemId),
                new { MemberId = memberId, Replacing = (Guid?)null },
                TestContext.Current.CancellationToken
            );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null((await factory.Connection.LoadItem(itemId))?.ReservedFor);
    }

    [Fact]
    public async Task MapEndpoint_ReturnsConflict_WhenSomeoneElseReservedTheItemFirst()
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
            .PutAsJsonAsync(
                Path(itemGroupId, itemId),
                new { MemberId = TestAuthHandler.UserId, Replacing = (Guid?)null },
                TestContext.Current.CancellationToken
            );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(otherMemberId, (await factory.Connection.LoadItem(itemId))?.ReservedFor);
    }

    // Two members claim the same free item at the same moment. Each round races the two
    // requests once: exactly one claim may win, or the item would change hands unseen.
    [Fact]
    public async Task MapEndpoint_ReservesTheItemForOneMember_WhenTwoClaimItAtOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid[] members = [Guid.NewGuid(), Guid.NewGuid()];
        HttpClient[] clients = [.. members.Select(factory.CreateClientFor)];

        for (int round = 0; round < 25; round++)
        {
            var itemGroupId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            await factory.Connection.InsertItemGroup(itemGroupId, members);
            await factory.Connection.InsertItem(itemGroupId, itemId);

            HttpResponseMessage[] responses = await Task.WhenAll(
                members.Select(
                    (member, i) =>
                        clients[i]
                            .PutAsJsonAsync(
                                Path(itemGroupId, itemId),
                                new { MemberId = member, Replacing = (Guid?)null },
                                ct
                            )
                )
            );

            int winner = Array.FindIndex(responses, r => r.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(members[winner], (await factory.Connection.LoadItem(itemId))?.ReservedFor);
        }
    }

    // One member reserves an item for another at the moment a third removes them. Whichever
    // goes first, the item must end up free: never reserved for someone outside the list.
    [Fact]
    public async Task MapEndpoint_NeverLeavesAnItemReservedForSomeoneWhoWasRemoved()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var reserver = Guid.NewGuid();
        var remover = Guid.NewGuid();
        var removed = Guid.NewGuid();
        HttpClient reserverClient = factory.CreateClientFor(reserver);
        HttpClient removerClient = factory.CreateClientFor(remover);

        for (int round = 0; round < 25; round++)
        {
            var itemGroupId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            await factory.Connection.InsertItemGroup(itemGroupId, reserver, remover, removed);
            await factory.Connection.InsertItem(itemGroupId, itemId);

            HttpResponseMessage[] responses = await Task.WhenAll(
                reserverClient.PutAsJsonAsync(
                    Path(itemGroupId, itemId),
                    new { MemberId = removed, Replacing = (Guid?)null },
                    ct
                ),
                removerClient.DeleteAsync($"/api/list/{itemGroupId}/member/{removed}", ct)
            );

            // The reservation came first and the removal freed the item, or the removal came
            // first and the reservation found them gone.
            Assert.True(
                responses[0].StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict,
                $"Unexpected status {responses[0].StatusCode} in round {round}"
            );
            Assert.Equal(HttpStatusCode.NoContent, responses[1].StatusCode);
            Assert.Null((await factory.Connection.LoadItem(itemId))?.ReservedFor);
        }
    }

    private static string Path(Guid itemGroupId, Guid itemId) =>
        $"/api/list/{itemGroupId}/{itemId}/reservation";
}
