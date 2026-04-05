using System.Collections.Concurrent;
using Core.AuditLog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Core.Tests.AuditLog;

/// <summary>
/// <para>Unit tests for ChannelAuditWriter.</para>
/// <para>
/// WriteBatchAsync opens a real SqlConnection, so tests use a connection string that
/// fails fast (port 19131 is not listening on any test machine — the OS returns
/// "connection refused" synchronously). All drain-path tests wait for the logged
/// warning that WriteBatchAsync emits on failure, using a SemaphoreSlim in the
/// capturing logger so tests never spin-wait or use arbitrary Task.Delay.
/// </para>
/// <para>
/// One test (DrainAsync_FlushesPartialBatch_AfterFiveSecondWindow) intentionally
/// waits ~5 s because the flush window is hardcoded in production code.
/// </para>
/// </summary>
public sealed class ChannelAuditWriterTests
{
    // Port 19131 is virtually guaranteed to refuse connections immediately on any
    // developer or CI machine, making WriteBatchAsync fail without blocking.
    private const string FailingConnectionString =
        "Server=localhost,19131;Connect Timeout=2;TrustServerCertificate=true;User Id=sa;Password=x";

    private static IConfiguration BuildConfig(string? connectionString = FailingConnectionString)
    {
        IEnumerable<KeyValuePair<string, string?>> pairs = connectionString is null
            ? []
            : [new KeyValuePair<string, string?>("ConnectionStrings:database", connectionString)];
        return new ConfigurationBuilder().AddInMemoryCollection(pairs).Build();
    }

    private static AuditEntry CreateEntry() =>
        new(
            Timestamp: DateTimeOffset.UtcNow,
            TraceId: null,
            UserId: null,
            IpAddress: null,
            ResourceType: "ItemGroup",
            Operation: "Test",
            ResourceId: null,
            SubResourceId: null,
            TargetUserId: null,
            Outcome: "Success",
            FailureReason: null
        );

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenConnectionStringIsNotConfigured()
    {
        IConfiguration config = BuildConfig(connectionString: null);
        Assert.Throws<InvalidOperationException>(() =>
            new ChannelAuditWriter(config, new CapturingLogger())
        );
    }

    [Fact]
    public void Constructor_Succeeds_WhenConnectionStringIsConfigured()
    {
        // Should not throw
        ChannelAuditWriter writer = new(BuildConfig(), new CapturingLogger());
        writer.Dispose();
    }

    // -------------------------------------------------------------------------
    // Enqueue
    // -------------------------------------------------------------------------

    [Fact]
    public void Enqueue_CompletesWithoutBlocking()
    {
        // Enqueue is a plain synchronous call — no async, no lock, no await.
        ChannelAuditWriter writer = new(BuildConfig(), new CapturingLogger());
        writer.Enqueue(CreateEntry());
        writer.Dispose();
    }

    [Fact]
    public async Task Enqueue_LogsWarning_WhenChannelIsClosedAfterStop()
    {
        // TryComplete() in StopAsync closes the channel writer; subsequent
        // TryWrite calls return false. With DropOldest, TryWrite only returns
        // false when the channel is closed — a lifecycle issue worth logging.
        var logger = new CapturingLogger();
        ChannelAuditWriter writer = new(BuildConfig(), logger);

        await writer.StartAsync(CancellationToken.None);
        await writer.StopAsync(CancellationToken.None);

        writer.Enqueue(CreateEntry()); // must not throw

        Assert.Single(logger.Warnings);
        Assert.Equal("Audit log channel is closed; entry was discarded.", logger.Warnings[0]);
        writer.Dispose();
    }

    // -------------------------------------------------------------------------
    // Shutdown flush (IHostedService.StopAsync)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StopAsync_FlushesRemainingEntries_BeforeReturning()
    {
        // The finally block in DrainAsync must flush entries that were batched but
        // not yet written when the cancellation token fires. StopAsync only returns
        // after DrainAsync completes, which means the flush is guaranteed before
        // this assert runs.
        var logger = new CapturingLogger();
        ChannelAuditWriter writer = new(BuildConfig(), logger);

        await writer.StartAsync(CancellationToken.None);

        const int count = 5;
        for (int i = 0; i < count; i++)
        {
            writer.Enqueue(CreateEntry());
        }

        await writer.StopAsync(CancellationToken.None);

        Assert.Single(logger.Warnings);
        Assert.Equal($"Failed to write {count} audit log entries.", logger.Warnings[0]);
    }

    [Fact]
    public async Task StopAsync_DoesNotThrow_WhenNeverStarted()
    {
        // cts is null and drainTask is Task.CompletedTask — StopAsync should
        // return cleanly without touching the CancellationTokenSource.
        ChannelAuditWriter writer = new(BuildConfig(), new CapturingLogger());
        await writer.StopAsync(CancellationToken.None); // should not throw
        writer.Dispose();
    }

    [Fact]
    public async Task StopAsync_DoesNotThrow_WhenCancellationTokenFiresBeforeDrainCompletes()
    {
        // drainTask.WaitAsync(cancellationToken) throws OperationCanceledException
        // when the host's shutdown deadline expires. StopAsync must swallow it.
        var logger = new CapturingLogger();
        ChannelAuditWriter writer = new(BuildConfig(), logger);

        await writer.StartAsync(CancellationToken.None);

        using var stopCts = new CancellationTokenSource();
        await stopCts.CancelAsync(); // pre-cancel to force the timeout path

        await writer.StopAsync(stopCts.Token); // must not throw
        writer.Dispose();
    }

    // -------------------------------------------------------------------------
    // Write failure handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task WriteBatchAsync_LogsWarning_WhenDatabaseConnectionFails()
    {
        var logger = new CapturingLogger();
        ChannelAuditWriter writer = new(BuildConfig(), logger);

        await writer.StartAsync(CancellationToken.None);
        writer.Enqueue(CreateEntry());

        // Use StopAsync to trigger the flush deterministically rather than
        // waiting for the 5-second window.
        await writer.StopAsync(CancellationToken.None);

        Assert.Single(logger.Warnings);
        Assert.Equal("Failed to write 1 audit log entries.", logger.Warnings[0]);
    }

    [Fact]
    public async Task DrainAsync_ContinuesDraining_AfterWriteFailure()
    {
        // After the first batch fails, the drain loop must continue to process
        // subsequent batches rather than exiting.
        var logger = new CapturingLogger();
        ChannelAuditWriter writer = new(BuildConfig(), logger);

        await writer.StartAsync(CancellationToken.None);

        // First batch of exactly 50 — flushed on the next PeriodicTimer tick (up to 5 seconds).
        for (int i = 0; i < 50; i++)
        {
            writer.Enqueue(CreateEntry());
        }
        await logger.WaitForWarningsAsync(count: 1, timeout: TimeSpan.FromSeconds(15));

        // Second batch of exactly 50 — must also be flushed after the first failure.
        for (int i = 0; i < 50; i++)
        {
            writer.Enqueue(CreateEntry());
        }
        // Wait for 1 more signal (the second flush), not 2 total from scratch.
        await logger.WaitForWarningsAsync(count: 1, timeout: TimeSpan.FromSeconds(15));

        await writer.StopAsync(CancellationToken.None);

        Assert.Equal(2, logger.Warnings.Count);
        Assert.All(logger.Warnings, w => Assert.Equal("Failed to write 50 audit log entries.", w));
    }

    // -------------------------------------------------------------------------
    // Batching behaviour
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DrainAsync_FlushesInBatchesOfFifty_WithinTimerWindow()
    {
        // A batch of 50 is flushed in a single WriteBatchAsync call on the next
        // PeriodicTimer tick (up to 5 seconds).
        var logger = new CapturingLogger();
        ChannelAuditWriter writer = new(BuildConfig(), logger);

        await writer.StartAsync(CancellationToken.None);

        for (int i = 0; i < 50; i++)
        {
            writer.Enqueue(CreateEntry());
        }

        // Timeout: 5s timer window + 2s connection timeout + buffer.
        await logger.WaitForWarningsAsync(count: 1, timeout: TimeSpan.FromSeconds(15));

        await writer.StopAsync(CancellationToken.None);

        Assert.Equal("Failed to write 50 audit log entries.", logger.Warnings[0]);
    }

    [Fact]
    public async Task DrainAsync_FlushesPartialBatch_AfterFiveSecondWindow()
    {
        // The 5-second flush window is hardcoded in production code.
        // This test necessarily waits approximately 5 seconds.
        var logger = new CapturingLogger();
        ChannelAuditWriter writer = new(BuildConfig(), logger);

        await writer.StartAsync(CancellationToken.None);

        writer.Enqueue(CreateEntry());
        writer.Enqueue(CreateEntry());

        // Timeout: 5s window + 2s connect timeout + buffer.
        await logger.WaitForWarningsAsync(count: 1, timeout: TimeSpan.FromSeconds(15));

        await writer.StopAsync(CancellationToken.None);

        Assert.Equal("Failed to write 2 audit log entries.", logger.Warnings[0]);
    }

    [Fact]
    public async Task DrainAsync_ExitsGracefully_WhenChannelCompletedWithNoEntries()
    {
        // When the channel is both complete (TryComplete) and empty,
        // WaitToReadAsync returns false rather than throwing OperationCanceledException.
        // DrainAsync must handle this branch and exit without writing anything.
        var logger = new CapturingLogger();
        ChannelAuditWriter writer = new(BuildConfig(), logger);

        await writer.StartAsync(CancellationToken.None);
        // No entries enqueued — DrainAsync is blocked at PeriodicTimer.WaitForNextTickAsync.
        // CancelAsync() in StopAsync causes WaitForNextTickAsync to throw OperationCanceledException.
        await writer.StopAsync(CancellationToken.None);

        Assert.Empty(logger.Warnings); // WriteBatchAsync was never called
        writer.Dispose();
    }

    // -------------------------------------------------------------------------
    // Disposal
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Dispose_DoesNotThrow_AfterStartAndStop()
    {
        ChannelAuditWriter writer = new(BuildConfig(), new CapturingLogger());
        await writer.StartAsync(CancellationToken.None);
        await writer.StopAsync(CancellationToken.None);
        writer.Dispose(); // should not throw
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenNeverStarted()
    {
        ChannelAuditWriter writer = new(BuildConfig(), new CapturingLogger());
        writer.Dispose(); // CancellationTokenSource is null — should not throw
    }
}

/// <summary>
/// Captures Warning-level log messages from ChannelAuditWriter.
/// Uses a counting SemaphoreSlim so tests can await a specific number of warnings
/// without races regardless of whether warnings arrive before or after the Wait call.
/// </summary>
internal sealed class CapturingLogger : ILogger<ChannelAuditWriter>, IDisposable
{
    private readonly SemaphoreSlim signal = new(0);
    private readonly ConcurrentBag<string> warnings = [];

    public IReadOnlyList<string> Warnings => [.. warnings];

    public async Task WaitForWarningsAsync(int count, TimeSpan timeout)
    {
        for (int i = 0; i < count; i++)
        {
            bool received = await signal.WaitAsync(timeout);
            if (!received)
            {
                throw new TimeoutException(
                    $"Expected {count} warning(s) within {timeout}, but only {i} arrived. "
                        + $"Warnings so far: [{string.Join(", ", warnings)}]"
                );
            }
        }
    }

    void ILogger.Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (logLevel < LogLevel.Warning)
        {
            return;
        }

        warnings.Add(formatter(state, exception));
        signal.Release();
    }

    bool ILogger.IsEnabled(LogLevel logLevel) => true;

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    public void Dispose() => signal.Dispose();
}
