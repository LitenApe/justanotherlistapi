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

        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/list/{itemGroupId}/member/{secondMemberId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
