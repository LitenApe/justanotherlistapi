using System.Security.Claims;

namespace Core
{
    public static class ClaimsPrincipalExtensionGetUserId
    {
        public static Guid? GetUserId(this ClaimsPrincipal user)
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value
                ?? user.FindFirst("user_id")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            try
            {
                return Guid.Parse(userId);
            }
            catch (FormatException)
            {
                // If the userId is not a valid Guid, return null
                return null;
            }
        }
    }
}
