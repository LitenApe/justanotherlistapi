using System.Security.Claims;

namespace Api;

public static class ClaimsPrincipalExtension
{
    /// <summary>
    /// The user ID in the token: the first of the NameIdentifier, <c>sub</c> and <c>user_id</c>
    /// claims that is present, or <see langword="null"/> when none is or it isn't a GUID.
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        string? userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst("user_id")?.Value;

        return Guid.TryParse(userId, out Guid id) ? id : null;
    }
}
