using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus;

/// <summary>Моніторинг глибини DLQ: gauge messaging.dlq.depth і попередження в логах.</summary>
public sealed class DlqDepthMonitor : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private readonly IConnection _connection;
    private readonly ConsumerQueue _queue;
    private readonly ILogger<DlqDepthMonitor> _logger;
    private long _depth;

    public DlqDepthMonitor(IConnection connection, ConsumerQueue queue, ILogger<DlqDepthMonitor> logger)
    {
        _connection = connection;
        _queue = queue;
        _logger = logger;

        // gauge читає останнє виміряне значення; тег queue має низьку кардинальність
        MessagingMetrics.Meter.CreateObservableGauge(
            "messaging.dlq.depth",
            () => new Measurement<long>(Interlocked.Read(ref _depth),
                new KeyValuePair<string, object?>("queue", _queue.DeadLetterQueue)),
            unit: "{message}",
            description: "Messages waiting in the dead letter queue");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // чекаємо, поки consumer оголосить топологію (DLQ має існувати)
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        do
        {
            await PollAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollAsync(CancellationToken ct)
    {
        try
        {
            await using var channel = await _connection.CreateChannelAsync(cancellationToken: ct);
            var info = await channel.QueueDeclarePassiveAsync(_queue.DeadLetterQueue, ct);

            Interlocked.Exchange(ref _depth, info.MessageCount);
            if (info.MessageCount > 0)
                _logger.LogWarning("DLQ {Queue} contains {Count} messages that need manual investigation",
                    _queue.DeadLetterQueue, info.MessageCount);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Could not read the depth of DLQ {Queue}", _queue.DeadLetterQueue);
        }
    }
}