using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus;

/// <summary>Передача контексту трасування (W3C traceparent) через заголовки повідомлень RabbitMQ.</summary>
public static class TracePropagation
{
    public const string SourceName = "Platform.Messaging";

    public static readonly ActivitySource Source = new(SourceName);
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    /// <summary>
    /// Producer-span публікації. Для повідомлень з outbox контекст відновлюється зі збереженого traceparent,
    /// тож публікація лишається в тому ж trace, що й HTTP-запит, який створив подію.
    /// </summary>
    public static Activity? StartPublishActivity(
        string exchange, string routingKey, string messageId, string eventType, string? parentTraceParent = null)
    {
        var name = $"{exchange} publish";

        Activity? activity;
        if (parentTraceParent is not null && ActivityContext.TryParse(parentTraceParent, null, out var parent))
            activity = Source.StartActivity(name, ActivityKind.Producer, parent);
        else
            activity = Source.StartActivity(name, ActivityKind.Producer);

        if (activity is null) return null;

        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.operation", "publish");
        activity.SetTag("messaging.destination.name", exchange);
        activity.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity.SetTag("messaging.message.id", messageId);
        activity.SetTag("event.type", eventType);
        return activity;
    }

    /// <summary>Записує traceparent / tracestate / baggage у заголовки повідомлення.</summary>
    public static void Inject(Activity? activity, BasicProperties props)
    {
        var current = activity ?? Activity.Current;
        if (current is null) return;

        props.Headers ??= new Dictionary<string, object?>();
        Propagator.Inject(new PropagationContext(current.Context, Baggage.Current), props.Headers,
            static (headers, key, value) => headers[key] = value);
    }

    /// <summary>Відновлює контекст із заголовків і створює Consumer-span як нащадка publish-span.</summary>
    public static Activity? StartConsumerActivity(BasicDeliverEventArgs ea, string queue, string? correlationId)
    {
        var parent = Propagator.Extract(default, ea.BasicProperties.Headers, GetHeaderValues);
        Baggage.Current = parent.Baggage;

        var activity = Source.StartActivity($"{queue} process", ActivityKind.Consumer, parent.ActivityContext);
        if (activity is null) return null;

        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.operation", "process");
        activity.SetTag("messaging.destination.name", queue);
        activity.SetTag("messaging.rabbitmq.destination.routing_key", ea.RoutingKey);
        activity.SetTag("messaging.message.id", ea.BasicProperties.MessageId);
        activity.SetTag("event.type", ea.BasicProperties.Type);

        // бізнес-ідентифікатор лишається доступним для логів і подальших публікацій
        if (!string.IsNullOrEmpty(correlationId))
            activity.SetTag(CorrelationContext.TagName, correlationId);

        return activity;
    }

    // рядкові заголовки AMQP приходять як byte[]
    private static IEnumerable<string> GetHeaderValues(IDictionary<string, object?>? headers, string key)
    {
        if (headers is not null && headers.TryGetValue(key, out var value))
        {
            return value switch
            {
                byte[] bytes => new[] { Encoding.UTF8.GetString(bytes) },
                string text => new[] { text },
                _ => Array.Empty<string>()
            };
        }

        return Array.Empty<string>();
    }
}