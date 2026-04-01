using System.Security.Claims;

internal static class TestHelpers
{
    public static ClaimsPrincipal CreatePrincipal(Guid userId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        return new ClaimsPrincipal(identity);
    }
}
