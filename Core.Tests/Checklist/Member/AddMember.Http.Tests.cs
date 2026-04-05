using System.Net;
using Dapper;

namespace Core.Tests.Checklist.MemberTests;

public sealed class AddMemberHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task MapEndpoint_ReturnsNoContent_OnHappyPath()
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
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            $"/api/list/{itemGroupId}/member/{newMemberId}",
            content: null
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
