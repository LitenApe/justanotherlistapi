using System.Security.Claims;
using Checklist.Domain;

namespace Api.Http;

internal static class CallerExtensions
{
    /// <summary>
    /// The header in which clients send their SignalR connection ID, so their own changes aren't
    /// sent back to them (see specifications/checklist.md, Real-Time Notifications).
    /// </summary>
    internal const string ConnectionIdHeader = "X-SignalR-Connection-Id";

    /// <summary>
    /// The caller behind a request, or <see langword="null"/> when its token carries no usable
    /// user ID; the endpoint then answers 401.
    /// </summary>
    internal static Caller? ToCaller(this ClaimsPrincipal user, HttpRequest request) =>
        user.GetUserId() is Guid userId
            ? new Caller(userId, request.Headers[ConnectionIdHeader])
            : null;
}
