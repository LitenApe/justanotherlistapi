using Checklist.Domain;

namespace Checklist.Ports;

/// <summary>Sends the emails the app sends people.</summary>
public interface IMailer
{
    /// <summary>
    /// Sends <paramref name="user"/> a link to choose a new password with <paramref name="code"/>.
    /// Failing to send is the adapter's to report: whoever asked must not learn from it whether
    /// the account exists.
    /// </summary>
    Task SendPasswordReset(User user, string code, CancellationToken ct);
}
