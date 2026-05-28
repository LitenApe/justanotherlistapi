using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Core.Tests;

internal static class TestHelpers
{
    public static ClaimsPrincipal CreatePrincipal(Guid userId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        return new ClaimsPrincipal(identity);
    }

    public static HttpRequest CreateHttpRequest()
    {
        return new DefaultHttpContext().Request;
    }

    public static CapturingNotifier CreateNotifier()
    {
        return new CapturingNotifier();
    }
}
