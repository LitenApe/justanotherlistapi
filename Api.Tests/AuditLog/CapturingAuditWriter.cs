using Api.AuditLog;

namespace Api.Tests;

internal sealed class CapturingAuditWriter : IAuditWriter
{
    public List<AuditEntry> Entries { get; } = [];

    public void Enqueue(AuditEntry entry) => Entries.Add(entry);
}
