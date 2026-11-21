using Checklist.Domain;

namespace Checklist.Ports;

/// <summary>Issues the bearer tokens that keep people signed in.</summary>
public interface ITokenIssuer
{
    /// <summary>A token that signs <paramref name="user"/> in until it expires.</summary>
    AccessToken Issue(User user);
}
