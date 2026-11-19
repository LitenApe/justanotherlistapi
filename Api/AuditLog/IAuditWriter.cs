namespace Api.AuditLog;

public interface IAuditWriter
{
    void Enqueue(AuditEntry entry);
}
