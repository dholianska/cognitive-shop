using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus;

/// <summary>Оголошує топологію під час старту застосунку (повторне оголошення безпечне).</summary>
public sealed class EventBusTopologyInitializer(IConnection connection, ILogger<EventBusTopologyInitializer> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await EventTopology.DeclareAllAsync(channel, ct);

        logger.LogInformation(
            "RabbitMQ topology declared: exchange {Exchange}, queues {Queues} (each with DLX and DLQ)",
            Exchanges.Catalog, string.Join(", ", EventTopology.All.Select(q => q.Queue)));
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}