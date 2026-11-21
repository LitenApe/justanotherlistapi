using System.Net;
using Checklist.Domain;

namespace Api.Tests.Checklist.MemberTests;

public sealed class AddMemberHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task MapEndpoint_AddsTheAccountByEmail_AndReturnsThemWithTheirName()
    {
        var itemGroupId = Guid.NewGuid();
        var kimId = Guid.NewGuid();
        string email = TestAccounts.UniqueEmail();
        await factory.Connection.InsertItemGroup(itemGroupId, ApiFactory.TestUserId);
        await factory.Connection.InsertUser(kimId, "Kim", email);

        HttpResponseMessage response = await AddMember(
            factory.CreateClient(),
            itemGroupId,
            email.ToUpperInvariant()
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new Member { Id = kimId, Name = "Kim" },
            await response.Content.ReadFromJsonAsync<Member>(TestContext.Current.CancellationToken)
        );
        Assert.Contains(kimId, await factory.Connection.LoadMembers(itemGroupId));
    }

    [Fact]
    public async Task MapEndpoint_Returns404_WhenNoAccountUsesTheEmail()
    {
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.InsertItemGroup(itemGroupId, ApiFactory.TestUserId);

        HttpResponseMessage response = await AddMember(
            factory.CreateClient(),
            itemGroupId,
            TestAccounts.UniqueEmail()
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(await factory.Connection.LoadMembers(itemGroupId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not an email")]
    public async Task MapEndpoint_Returns400_ForSomethingThatIsntAnEmail(string? email)
    {
        HttpResponseMessage response = await AddMember(
            factory.CreateClient(),
            Guid.NewGuid(),
            email
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Two members add the same person at the same moment. One request adds them; the other finds
    // them already there and gets 409, never a server error from the primary key. Each round
    // races the two requests once.
    [Fact]
    public async Task MapEndpoint_AddsThePersonOnce_WhenTwoMembersAddThemAtOnce()
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
            string email = TestAccounts.UniqueEmail();
            await factory.Connection.InsertItemGroup(itemGroupId, first, second);
            await factory.Connection.InsertUser(newMemberId, "Kim", email);

            HttpResponseMessage[] responses = await Task.WhenAll(
                AddMember(firstClient, itemGroupId, email),
                AddMember(secondClient, itemGroupId, email)
            );

            Assert.Equal(3, (await factory.Connection.LoadMembers(itemGroupId)).Count);
            Assert.Equal(
                new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
                responses.Select(r => r.StatusCode).Order()
            );
        }
    }

    private static Task<HttpResponseMessage> AddMember(
        HttpClient client,
        Guid itemGroupId,
        string? email
    ) =>
        client.PostAsJsonAsync(
            $"/api/list/{itemGroupId}/member",
            new { Email = email },
            TestContext.Current.CancellationToken
        );
}
