using System.Security.Claims;

namespace Core
{
    public static class ClaimsPrincipalExtensionGetUserId
    {
        public static string? GetUserId(this ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value
                ?? user.FindFirst("user_id")?.Value;
        }
    }
}
