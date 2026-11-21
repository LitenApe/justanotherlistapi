using System.Net;
using Checklist.Domain;
using Dapper;

namespace Api.Tests.Checklist.MemberTests;

public sealed class AddMemberHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task MapEndpoint_Returns200WithTheNewMember_OnHappyPath()
    {
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Test Group" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        var newMemberId = Guid.NewGuid();
        await factory.Connection.InsertUser(newMemberId, "Kim");
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            $"/api/list/{itemGroupId}/member/{newMemberId}",
            content: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new Member { Id = newMemberId, Name = "Kim" },
            await response.Content.ReadFromJsonAsync<Member>(TestContext.Current.CancellationToken)
        );
    }

    // Two members add the same user at the same moment. One request adds them; the other finds
    // them already there and gets 409, never a server error from the primary key. Each round
    // races the two requests once.
    [Fact]
    public async Task MapEndpoint_AddsTheUserOnce_WhenTwoMembersAddThemAtOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        HttpClient firstClient = factory.CreateClientFor(first);
        HttpClient secondClient = factory.CreateClientFor(second);

        for (int round = 0; round < 25; round++)
        {
            var itemGroupId = Guid.NewGuid();
            var newMemberId = Guid.NewGuid();
            await factory.Connection.InsertItemGroup(itemGroupId, first, second);

            HttpResponseMessage[] responses = await Task.WhenAll(
                firstClient.PostAsync(
                    $"/api/list/{itemGroupId}/member/{newMemberId}",
                    content: null,
                    ct
                ),
                secondClient.PostAsync(
                    $"/api/list/{itemGroupId}/member/{newMemberId}",
                    content: null,
                    ct
                )
            );

            Assert.Equal(3, (await factory.Connection.LoadMembers(itemGroupId)).Count);
            Assert.Equal(
                new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
                responses.Select(r => r.StatusCode).Order()
            );
        }
    }
}
