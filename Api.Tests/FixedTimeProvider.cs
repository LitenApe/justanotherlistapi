namespace Api.Tests;

/// <summary>A clock that always says <paramref name="now"/>, for use cases that stamp times.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
