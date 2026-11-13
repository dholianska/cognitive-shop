using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Platform.EventBus;

public static class EventBusExtensions
{
    /// <summary>Оголошення exchanges, черг, bindings і DLX/DLQ під час старту сервісу.</summary>
    public static IServiceCollection AddEventBusTopology(this IServiceCollection services)
        => services.AddHostedService<EventBusTopologyInitializer>();

    /// <summary>Publisher подій (singleton; IConnection дає Aspire.RabbitMQ.Client).</summary>
    public static IServiceCollection AddEventPublisher(this IServiceCollection services)
        => services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

    /// <summary>Моніторинг глибини DLQ черги сервісу-споживача (метрика messaging.dlq.depth).</summary>
    public static IServiceCollection AddDlqDepthMonitor(this IServiceCollection services, ConsumerQueue queue)
        => services.AddHostedService(sp => new DlqDepthMonitor(
            sp.GetRequiredService<IConnection>(), queue, sp.GetRequiredService<ILogger<DlqDepthMonitor>>()));
}