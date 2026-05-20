using System.Data;
using System.Security.Claims;

namespace Core.Checklist;

internal static class ChecklistAuthorizationExtensions
{
    internal static async Task<TResult> ExecuteAsAuthenticatedUser<TResult>(
        this ClaimsPrincipal claimsPrincipal,
        Func<Guid, Task<TResult>> onAuthorized,
        TResult onUnauthorized
    )
    {
        Guid? userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return onUnauthorized;
        }

        return await onAuthorized(userId.Value);
    }

    internal static async Task<TResult> ExecuteAsItemGroupMember<TResult>(
        this IDbConnection db,
        Guid itemGroupId,
        ClaimsPrincipal claimsPrincipal,
        Func<Guid, Task<TResult>> onAuthorized,
        TResult onUnauthorized,
        TResult onForbidden,
        CancellationToken ct = default
    )
    {
        Guid? userId = claimsPrincipal.GetUserId();
        if (userId is null)
        {
            return onUnauthorized;
        }

        bool isMember = await db.IsMember(itemGroupId, userId, ct);
        if (!isMember)
        {
            return onForbidden;
        }

        return await onAuthorized(userId.Value);
    }
}
