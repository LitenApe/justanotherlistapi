using System.Security.Claims;
using Api.Http;
using Checklist.Domain;
using Microsoft.AspNetCore.Http;

namespace Api.Tests.Http;

public sealed class CallerExtensionsTests
{
    [Fact]
    public void ToCaller_ReadsTheUserIdAndTheConnectionId()
    {
        var userId = Guid.NewGuid();
        HttpRequest request = TestHelpers.CreateHttpRequest();
        request.Headers[CallerExtensions.ConnectionIdHeader] = "conn-123";

        Caller? caller = TestHelpers.CreatePrincipal(userId).ToCaller(request);

        Assert.Equal(new Caller(userId, "conn-123"), caller);
    }

    [Fact]
    public void ToCaller_LeavesTheConnectionIdOut_WithoutTheHeader()
    {
        var userId = Guid.NewGuid();

        Caller? caller = TestHelpers
            .CreatePrincipal(userId)
            .ToCaller(TestHelpers.CreateHttpRequest());

        Assert.Equal(new Caller(userId), caller);
    }

    [Fact]
    public void ToCaller_ReturnsNull_WhenTheTokenHasNoUserId()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Null(anonymous.ToCaller(TestHelpers.CreateHttpRequest()));
    }
}
