using System.Net;
using System.Net.Http.Json;
using Core.Checklist;
using Dapper;

namespace Core.Tests.Checklist.ItemGroupTests;

public sealed class UpdateItemGroupSettingsHttpTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task MapEndpoint_ReturnsSettings_OnHappyPath()
    {
        Guid itemGroupId = await InsertGroupWithTestUser();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}/settings",
            new { AutoDeleteAfterMinutes = 1440, HideCompletedAfterMinutes = 0 },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ItemGroupSettings? settings = await response.Content.ReadFromJsonAsync<ItemGroupSettings>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(settings);
        Assert.Equal(1440, settings.AutoDeleteAfterMinutes);
        Assert.Equal(0, settings.HideCompletedAfterMinutes);
        Assert.NotNull(settings.AutoDeleteUpdatedAt);
    }

    [Fact]
    public async Task MapEndpoint_ReturnsBadRequest_WhenAutoDeleteIsShorterThanADay()
    {
        Guid itemGroupId = await InsertGroupWithTestUser();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}/settings",
            new { AutoDeleteAfterMinutes = 60 },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetItemGroup_IncludesSettings()
    {
        Guid itemGroupId = await InsertGroupWithTestUser();
        HttpClient client = factory.CreateClient();
        HttpResponseMessage update = await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}/settings",
            new { HideCompletedAfterMinutes = 30 },
            TestContext.Current.CancellationToken
        );
        update.EnsureSuccessStatusCode();

        ItemGroup? group = await client.GetFromJsonAsync<ItemGroup>(
            $"/api/list/{itemGroupId}",
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(group);
        Assert.Null(group.Settings.AutoDeleteAfterMinutes);
        Assert.Equal(30, group.Settings.HideCompletedAfterMinutes);
    }

    private async Task<Guid> InsertGroupWithTestUser()
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
        return itemGroupId;
    }
}
