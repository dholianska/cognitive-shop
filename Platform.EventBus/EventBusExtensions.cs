using Microsoft.Extensions.DependencyInjection;

namespace Platform.EventBus;

public static class EventBusExtensions
{
    /// <summary>Оголошення exchanges, черг, bindings і DLX/DLQ під час старту сервісу.</summary>
    public static IServiceCollection AddEventBusTopology(this IServiceCollection services)
        => services.AddHostedService<EventBusTopologyInitializer>();

    /// <summary>Publisher подій (singleton; IConnection дає Aspire.RabbitMQ.Client).</summary>
    public static IServiceCollection AddEventPublisher(this IServiceCollection services)
        => services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
}