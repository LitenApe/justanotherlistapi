using System.Net;
using Dapper;

namespace Core.Tests.Checklist.MemberTests;

public sealed class RemoveMemberHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task MapEndpoint_ReturnsNoContent_OnHappyPath()
    {
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Test Group" }
        );

        // Seed two members so that removing one is not removing the last member
        var secondMemberId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            """
            INSERT INTO Members (MemberId, ItemGroupId) VALUES (@Member1, @ItemGroupId);
            INSERT INTO Members (MemberId, ItemGroupId) VALUES (@Member2, @ItemGroupId);
            """,
            new
            {
                Member1 = TestAuthHandler.UserId,
                Member2 = secondMemberId,
                ItemGroupId = itemGroupId,
            }
        );

        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.DeleteAsync(
            $"/api/list/{itemGroupId}/member/{secondMemberId}",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // The two members of a group remove each other, or both leave, at the same moment. Only
    // one removal may go through: if both did, the group would have no members and nobody could
    // reach it again. Each round races the two requests once.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapEndpoint_KeepsOneMember_WhenBothMembersAreRemovedAtOnce(
        bool removeThemselves
    )
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        Guid firstRemoves = removeThemselves ? first : second;
        Guid secondRemoves = removeThemselves ? second : first;
        HttpClient firstClient = factory.CreateClientFor(first);
        HttpClient secondClient = factory.CreateClientFor(second);

        for (int round = 0; round < 25; round++)
        {
            var itemGroupId = Guid.NewGuid();
            await factory.Connection.ExecuteAsync(
                """
                INSERT INTO ItemGroups (Id, Name) VALUES (@ItemGroupId, 'Group');
                INSERT INTO Members (MemberId, ItemGroupId)
                VALUES (@First, @ItemGroupId), (@Second, @ItemGroupId);
                """,
                new
                {
                    ItemGroupId = itemGroupId,
                    First = first,
                    Second = second,
                }
            );

            HttpResponseMessage[] responses = await Task.WhenAll(
                firstClient.DeleteAsync($"/api/list/{itemGroupId}/member/{firstRemoves}", ct),
                secondClient.DeleteAsync($"/api/list/{itemGroupId}/member/{secondRemoves}", ct)
            );

            int remaining = await factory.Connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Members WHERE ItemGroupId = @ItemGroupId",
                new { ItemGroupId = itemGroupId }
            );
            Assert.Equal(1, remaining);
            // The other request is refused: its target is now the last member (409), or its
            // caller was removed first (403).
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
            Assert.All(responses, r =>
                Assert.True(
                    r.StatusCode
                        is HttpStatusCode.NoContent
                            or HttpStatusCode.Conflict
                            or HttpStatusCode.Forbidden,
                    $"Unexpected status {r.StatusCode} in round {round}"
                )
            );
        }
    }
}
