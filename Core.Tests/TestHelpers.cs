using System.Security.Claims;
using Core.Checklist;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>
    /// The cleanup service connects with the app's connection string on a timer, which tests
    /// don't provide. Tests call <see cref="AutoDeleteItemGroups"/> directly instead.
    /// </summary>
    public static void RemoveChecklistCleanupService(this IServiceCollection services)
    {
        services.Remove(
            services.Single(d => d.ImplementationType == typeof(ChecklistCleanupService))
        );
    }
}
