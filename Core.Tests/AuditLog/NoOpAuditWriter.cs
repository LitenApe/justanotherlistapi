using Core.AuditLog;

namespace Core.Tests;

internal sealed class NoOpAuditWriter : IAuditWriter
{
    public void Enqueue(AuditEntry entry) { }
}
