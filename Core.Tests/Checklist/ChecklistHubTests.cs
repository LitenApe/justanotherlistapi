using System.Net.Http.Json;
using Core.Checklist;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Core.Tests.Checklist;

public sealed class ChecklistHubTests(SignalRApiFactory factory) : IClassFixture<SignalRApiFactory>
{
    [Fact]
    public async Task JoinGroup_Succeeds_WhenUserIsMember()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Hub Test Group" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        await using HubConnection connection = CreateHubConnection();
        await connection.StartAsync();

        // Act & Assert — should not throw
        await connection.InvokeAsync("JoinGroup", itemGroupId);
    }

    [Fact]
    public async Task JoinGroup_ThrowsHubException_WhenUserIsNotMember()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Forbidden Group" }
        );
        // No member row for TestAuthHandler.UserId

        await using HubConnection connection = CreateHubConnection();
        await connection.StartAsync();

        // Act & Assert
        await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync("JoinGroup", itemGroupId)
        );
    }

    [Fact]
    public async Task LeaveGroup_Succeeds_Always()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Leave Test Group" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        await using HubConnection connection = CreateHubConnection();
        await connection.StartAsync();
        await connection.InvokeAsync("JoinGroup", itemGroupId);

        // Act & Assert — should not throw
        await connection.InvokeAsync("LeaveGroup", itemGroupId);
    }

    [Fact]
    public async Task ItemCreated_IsBroadcastToGroupMembers()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Broadcast Test" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        await using HubConnection connection = CreateHubConnection();
        await connection.StartAsync();
        await connection.InvokeAsync("JoinGroup", itemGroupId);

        var tcs = new TaskCompletionSource<(Guid GroupId, Item Item)>();
        connection.On<Guid, Item>(
            "ItemCreated",
            (groupId, item) => tcs.TrySetResult((groupId, item))
        );

        // Act — create item via HTTP
        using HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/list/{itemGroupId}",
            new { Name = "Broadcast Item" }
        );
        response.EnsureSuccessStatusCode();

        // Assert — notification received within timeout
        Task completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Equal(tcs.Task, completed);

        (Guid receivedGroupId, Item receivedItem) = await tcs.Task;
        Assert.Equal(itemGroupId, receivedGroupId);
        Assert.Equal("Broadcast Item", receivedItem.Name);
    }

    [Fact]
    public async Task ItemUpdated_IsBroadcastToGroupMembers()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Update Broadcast" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
            new
            {
                Id = itemId,
                Name = "Old",
                Description = "Old Desc",
                IsComplete = false,
                ItemGroupId = itemGroupId,
            }
        );

        await using HubConnection connection = CreateHubConnection();
        await connection.StartAsync();
        await connection.InvokeAsync("JoinGroup", itemGroupId);

        var tcs = new TaskCompletionSource<(Guid GroupId, Item Item)>();
        connection.On<Guid, Item>(
            "ItemUpdated",
            (groupId, item) => tcs.TrySetResult((groupId, item))
        );

        // Act
        using HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}/{itemId}",
            new
            {
                Name = "Updated",
                Description = "Updated Desc",
                IsComplete = true,
            }
        );
        response.EnsureSuccessStatusCode();

        // Assert
        Task completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Equal(tcs.Task, completed);

        (Guid receivedGroupId, Item receivedItem) = await tcs.Task;
        Assert.Equal(itemGroupId, receivedGroupId);
        Assert.Equal("Updated", receivedItem.Name);
        Assert.True(receivedItem.IsComplete);
    }

    [Fact]
    public async Task ItemDeleted_IsBroadcastToGroupMembers()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Delete Broadcast" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Items (Id, Name, Description, IsComplete, ItemGroupId) VALUES (@Id, @Name, @Description, @IsComplete, @ItemGroupId)",
            new
            {
                Id = itemId,
                Name = "ToDelete",
                Description = (string?)null,
                IsComplete = false,
                ItemGroupId = itemGroupId,
            }
        );

        await using HubConnection connection = CreateHubConnection();
        await connection.StartAsync();
        await connection.InvokeAsync("JoinGroup", itemGroupId);

        var tcs = new TaskCompletionSource<(Guid GroupId, Guid ItemId)>();
        connection.On<Guid, Guid>(
            "ItemDeleted",
            (groupId, deletedItemId) => tcs.TrySetResult((groupId, deletedItemId))
        );

        // Act
        using HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.DeleteAsync(
            $"/api/list/{itemGroupId}/{itemId}"
        );
        response.EnsureSuccessStatusCode();

        // Assert
        Task completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Equal(tcs.Task, completed);

        (Guid receivedGroupId, Guid receivedItemId) = await tcs.Task;
        Assert.Equal(itemGroupId, receivedGroupId);
        Assert.Equal(itemId, receivedItemId);
    }

    [Fact]
    public async Task GroupRenamed_IsBroadcastToGroupMembers()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Old Name" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        await using HubConnection connection = CreateHubConnection();
        await connection.StartAsync();
        await connection.InvokeAsync("JoinGroup", itemGroupId);

        var tcs = new TaskCompletionSource<(Guid GroupId, string Name)>();
        connection.On<Guid, string>(
            "GroupRenamed",
            (groupId, name) => tcs.TrySetResult((groupId, name))
        );

        // Act
        using HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/list/{itemGroupId}",
            new { Name = "New Name" }
        );
        response.EnsureSuccessStatusCode();

        // Assert
        Task completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Equal(tcs.Task, completed);

        (Guid receivedGroupId, string receivedName) = await tcs.Task;
        Assert.Equal(itemGroupId, receivedGroupId);
        Assert.Equal("New Name", receivedName);
    }

    [Fact]
    public async Task GroupDeleted_IsBroadcastToGroupMembers()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "ToDelete" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        await using HubConnection connection = CreateHubConnection();
        await connection.StartAsync();
        await connection.InvokeAsync("JoinGroup", itemGroupId);

        var tcs = new TaskCompletionSource<Guid>();
        connection.On<Guid>("GroupDeleted", groupId => tcs.TrySetResult(groupId));

        // Act
        using HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.DeleteAsync($"/api/list/{itemGroupId}");
        response.EnsureSuccessStatusCode();

        // Assert
        Task completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Equal(tcs.Task, completed);

        Guid receivedGroupId = await tcs.Task;
        Assert.Equal(itemGroupId, receivedGroupId);
    }

    [Fact]
    public async Task MemberAdded_IsBroadcastToGroupMembers()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Member Add Broadcast" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        await using HubConnection connection = CreateHubConnection();
        await connection.StartAsync();
        await connection.InvokeAsync("JoinGroup", itemGroupId);

        var tcs = new TaskCompletionSource<(Guid GroupId, Guid MemberId)>();
        connection.On<Guid, Guid>(
            "MemberAdded",
            (groupId, memberId) => tcs.TrySetResult((groupId, memberId))
        );

        // Act
        using HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync(
            $"/api/list/{itemGroupId}/member/{newMemberId}",
            null
        );
        response.EnsureSuccessStatusCode();

        // Assert
        Task completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Equal(tcs.Task, completed);

        (Guid receivedGroupId, Guid receivedMemberId) = await tcs.Task;
        Assert.Equal(itemGroupId, receivedGroupId);
        Assert.Equal(newMemberId, receivedMemberId);
    }

    [Fact]
    public async Task MemberRemoved_IsBroadcastToGroupMembers()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        var memberToRemove = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Member Remove Broadcast" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = memberToRemove, ItemGroupId = itemGroupId }
        );

        await using HubConnection connection = CreateHubConnection();
        await connection.StartAsync();
        await connection.InvokeAsync("JoinGroup", itemGroupId);

        var tcs = new TaskCompletionSource<(Guid GroupId, Guid MemberId)>();
        connection.On<Guid, Guid>(
            "MemberRemoved",
            (groupId, memberId) => tcs.TrySetResult((groupId, memberId))
        );

        // Act
        using HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.DeleteAsync(
            $"/api/list/{itemGroupId}/member/{memberToRemove}"
        );
        response.EnsureSuccessStatusCode();

        // Assert
        Task completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Equal(tcs.Task, completed);

        (Guid receivedGroupId, Guid receivedMemberId) = await tcs.Task;
        Assert.Equal(itemGroupId, receivedGroupId);
        Assert.Equal(memberToRemove, receivedMemberId);
    }

    [Fact]
    public async Task ExcludeConnectionId_PreventsNotificationToSender()
    {
        // Arrange
        var itemGroupId = Guid.NewGuid();
        await factory.Connection.ExecuteAsync(
            "INSERT INTO ItemGroups (Id, Name) VALUES (@Id, @Name)",
            new { Id = itemGroupId, Name = "Exclude Test" }
        );
        await factory.Connection.ExecuteAsync(
            "INSERT INTO Members (MemberId, ItemGroupId) VALUES (@MemberId, @ItemGroupId)",
            new { MemberId = TestAuthHandler.UserId, ItemGroupId = itemGroupId }
        );

        await using HubConnection connection = CreateHubConnection();
        await connection.StartAsync();
        await connection.InvokeAsync("JoinGroup", itemGroupId);

        string? hubConnectionId = connection.ConnectionId;
        Assert.NotNull(hubConnectionId);

        bool received = false;
        connection.On<Guid, Item>("ItemCreated", (_, _) => received = true);

        // Act — send with X-SignalR-Connection-Id matching the connected client
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/list/{itemGroupId}");
        request.Content = JsonContent.Create(new { Name = "Excluded Item" });
        request.Headers.Add("X-SignalR-Connection-Id", hubConnectionId);
        HttpResponseMessage response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // Assert — the sender should NOT receive the notification
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        Assert.False(received);
    }

    private HubConnection CreateHubConnection()
    {
        return new HubConnectionBuilder()
            .WithUrl(
                $"{factory.Server.BaseAddress}hubs/checklist",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                }
            )
            .Build();
    }
}
