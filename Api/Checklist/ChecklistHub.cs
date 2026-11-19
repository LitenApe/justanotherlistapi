using Checklist.Domain;
using Checklist.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Checklist;

[Authorize]
public sealed class ChecklistHub(SubscribeToItemGroup subscribe) : Hub<IChecklistClient>
{
    public async Task JoinGroup(Guid groupId)
    {
        Guid? userId = Context.User?.GetUserId();
        if (
            userId is not null
            && await subscribe.Execute(
                new Caller(userId.Value, Context.ConnectionId),
                groupId,
                Context.ConnectionAborted
            )
        )
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupId.ToString());
            return;
        }

        throw new HubException("Forbidden");
    }

    public async Task LeaveGroup(Guid groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId.ToString());
    }
}
