namespace Core.AuditLog;

public interface IAuditWriter
{
    void Enqueue(AuditEntry entry);
}
