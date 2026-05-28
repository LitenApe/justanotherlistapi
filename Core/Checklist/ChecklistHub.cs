using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Core.Checklist;

[Authorize]
public sealed class ChecklistHub(IDbConnection db) : Hub<IChecklistClient>
{
    public async Task JoinGroup(Guid groupId)
    {
        Guid? userId = Context.User?.GetUserId();
        if (userId is not null)
        {
            bool isMember = await db.IsMember(groupId, userId, Context.ConnectionAborted);
            if (isMember)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupId.ToString());
                return;
            }
        }

        throw new HubException("Forbidden");
    }

    public async Task LeaveGroup(Guid groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId.ToString());
    }
}
