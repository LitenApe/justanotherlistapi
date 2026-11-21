using System.Collections.Concurrent;
using Checklist.Domain;
using Checklist.Ports;

namespace Api.Tests;

/// <summary>Keeps the emails the app would have sent, so tests can read the reset codes.</summary>
internal sealed class CapturingMailer : IMailer
{
    public ConcurrentQueue<SentPasswordReset> PasswordResets { get; } = new();

    public Task SendPasswordReset(User user, string code, CancellationToken ct)
    {
        PasswordResets.Enqueue(new SentPasswordReset(user, code));
        return Task.CompletedTask;
    }

    /// <summary>The code in the last reset email sent to <paramref name="email"/>.</summary>
    public string LastCodeFor(string email) =>
        PasswordResets
            .LastOrDefault(sent =>
                string.Equals(sent.User.Email, email, StringComparison.OrdinalIgnoreCase)
            )
            ?.Code
        ?? throw new InvalidOperationException($"No reset email went to {email}.");

    public sealed record SentPasswordReset(User User, string Code);
}
