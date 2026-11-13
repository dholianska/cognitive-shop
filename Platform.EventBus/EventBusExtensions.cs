using Microsoft.Extensions.DependencyInjection;

namespace Platform.EventBus;

public static class EventBusExtensions
{
    /// <summary>Оголошення exchanges, черг, bindings і DLX/DLQ під час старту сервісу.</summary>
    public static IServiceCollection AddEventBusTopology(this IServiceCollection services)
        => services.AddHostedService<EventBusTopologyInitializer>();
}