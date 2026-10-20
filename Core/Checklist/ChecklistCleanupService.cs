using Core.AuditLog;
using Microsoft.Data.SqlClient;

namespace Core.Checklist;

/// <summary>
/// Runs <see cref="AutoDeleteItemGroups"/> once a minute. Uses its own connection because the
/// request-scoped one isn't available outside a request.
/// </summary>
internal sealed partial class ChecklistCleanupService(
    IConfiguration configuration,
    IChecklistNotifier notifier,
    IAuditWriter auditWriter,
    TimeProvider timeProvider,
    ILogger<ChecklistCleanupService> logger
) : BackgroundService
{
    private static readonly TimeSpan interval = TimeSpan.FromMinutes(1);

    private readonly string connectionString =
        configuration.GetConnectionString("database")
        ?? throw new InvalidOperationException("Connection string 'database' is not configured.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                IReadOnlyList<Guid> deleted = await AutoDeleteItemGroups.Execute(
                    connection,
                    notifier,
                    auditWriter,
                    timeProvider.GetUtcNow(),
                    stoppingToken
                );
                if (deleted.Count > 0)
                {
                    LogDeleted(logger, deleted.Count);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Auto-deleted {Count} completed lists.")]
    private static partial void LogDeleted(ILogger logger, int count);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Auto-deleting completed lists failed; retrying on the next run."
    )]
    private static partial void LogFailed(ILogger logger, Exception ex);
}
