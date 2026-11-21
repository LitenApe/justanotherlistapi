namespace Checklist.Domain;

/// <summary>Someone who just signed up or signed in, and the token that keeps them signed in.</summary>
public sealed record Session(AccessToken Token, User User);
