using System.Threading.Channels;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Core.AuditLog;

internal sealed partial class ChannelAuditWriter : IAuditWriter, IHostedService, IDisposable
{
    private readonly Channel<AuditEntry> channel;
    private readonly string connectionString;
    private readonly ILogger<ChannelAuditWriter> logger;
    private CancellationTokenSource? cts;
    private Task drainTask = Task.CompletedTask;

    public ChannelAuditWriter(IConfiguration configuration, ILogger<ChannelAuditWriter> logger)
    {
        this.logger = logger;
        connectionString =
            configuration.GetConnectionString("database")
            ?? throw new InvalidOperationException(
                "Connection string 'database' is not configured."
            );
        channel = Channel.CreateBounded<AuditEntry>(
            new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            }
        );
    }

    public void Enqueue(AuditEntry entry)
    {
        if (!channel.Writer.TryWrite(entry))
        {
            LogEntryDropped(logger);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cts = new CancellationTokenSource();
        drainTask = Task.Run(() => DrainAsync(cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        channel.Writer.TryComplete();
        if (cts is not null)
        {
            await cts.CancelAsync();
        }
        try
        {
            await drainTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose() => cts?.Dispose();

    private async Task DrainAsync(CancellationToken ct)
    {
        var batch = new List<AuditEntry>(50);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                while (channel.Reader.TryRead(out AuditEntry? entry))
                {
                    batch.Add(entry);
                    if (batch.Count == 50)
                    {
                        await WriteBatchAsync(batch);
                        batch.Clear();
                    }
                }
                if (batch.Count > 0)
                {
                    await WriteBatchAsync(batch);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            // Write any entries remaining in the channel
            while (channel.Reader.TryRead(out AuditEntry? remaining))
            {
                batch.Add(remaining);
            }
            if (batch.Count > 0)
            {
                await WriteBatchAsync(batch);
            }
        }
    }

    private async Task WriteBatchAsync(List<AuditEntry> batch)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync(
                """
                INSERT INTO AuditLog
                    (Timestamp, TraceId, UserId, IpAddress, ResourceType, Operation,
                     ResourceId, SubResourceId, TargetUserId, Outcome, FailureReason)
                VALUES
                    (@Timestamp, @TraceId, @UserId, @IpAddress, @ResourceType, @Operation,
                     @ResourceId, @SubResourceId, @TargetUserId, @Outcome, @FailureReason)
                """,
                batch
            );
        }
        catch (Exception ex)
        {
            LogWriteFailed(logger, ex, batch.Count);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Audit log channel is closed; entry was discarded."
    )]
    private static partial void LogEntryDropped(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to write {Count} audit log entries."
    )]
    private static partial void LogWriteFailed(ILogger logger, Exception ex, int count);
}
