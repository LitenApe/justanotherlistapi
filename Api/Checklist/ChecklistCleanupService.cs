using Checklist.UseCases;

namespace Api.Checklist;

/// <summary>
/// Runs the <see cref="AutoDeleteItemGroups"/> use case once a minute. Each run gets a DI scope of
/// its own, and so its own SQL connection, the way a request does.
/// </summary>
internal sealed partial class ChecklistCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ChecklistCleanupService> logger
) : BackgroundService
{
    private static readonly TimeSpan interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IReadOnlyList<Guid> deleted = await scope
                    .ServiceProvider.GetRequiredService<AutoDeleteItemGroups>()
                    .Execute(stoppingToken);
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
