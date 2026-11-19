namespace Checklist.Domain;

/// <summary>
/// Who is asking: the signed-in user, and the realtime connection the request came from, if
/// any. Use cases only pass <paramref name="ConnectionId"/> on to the notifier, so the sender
/// doesn't get its own change back; they never interpret it.
/// </summary>
/// <param name="UserId">The signed-in user.</param>
/// <param name="ConnectionId">The caller's SignalR connection, or <see langword="null"/>.</param>
public sealed record Caller(Guid UserId, string? ConnectionId = null);
