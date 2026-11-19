using Api.AuditLog;

namespace Api.Tests;

internal sealed class NoOpAuditWriter : IAuditWriter
{
    public void Enqueue(AuditEntry entry) { }
}
