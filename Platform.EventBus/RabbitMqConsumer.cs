using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;

namespace Platform.EventBus;

/// <summary>Постійна (непоправна) помилка: повторна спроба не допоможе, повідомлення йде в DLQ.</summary>
public sealed class PermanentFailureException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Базовий споживач черги: BackgroundService з ручним підтвердженням (autoAck: false).
/// Успіх → Ack; постійний збій → Nack без requeue (DLX → DLQ). Конкретний сервіс реалізує лише HandleAsync.
/// </summary>
public abstract class RabbitMqConsumer(IConnection connection, ConsumerQueue queue, ILogger logger)
    : BackgroundService
{
    private IChannel? _channel;

    protected ILogger Logger => logger;
    protected ConsumerQueue Queue => queue;
    protected IChannel Channel => _channel ?? throw new InvalidOperationException("Consumer channel is not started");

    /// <summary>Скільки непідтверджених повідомлень споживач може мати одночасно.</summary>
    protected virtual ushort PrefetchCount => 10;

    /// <summary>Обробляє подію за її типом (BasicProperties.Type). Кидає виняток, якщо обробка не вдалась.</summary>
    protected abstract Task HandleAsync(string eventType, ReadOnlyMemory<byte> body, CancellationToken ct);

    protected static T Deserialize<T>(ReadOnlyMemory<byte> body) where T : IntegrationEvent
        => JsonSerializer.Deserialize<T>(body.Span, JsonDefaults.Options)
           ?? throw new JsonException("Empty message body");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // топологія оголошується під час старту (повторне оголошення безпечне)
        await EventTopology.DeclareConsumerQueueAsync(_channel, queue, stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: PrefetchCount, global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, ea) => OnMessageAsync(ea, stoppingToken);

        await _channel.BasicConsumeAsync(queue.Queue, autoAck: false, consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("Consumer started: queue {Queue}, prefetch {Prefetch}", queue.Queue, PrefetchCount);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);   // скасовує stoppingToken

        if (_channel is not null)
        {
            try
            {
                await _channel.CloseAsync(cancellationToken);   // непідтверджені повідомлення повернуться в чергу
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Closing the channel of queue {Queue} failed", queue.Queue);
            }
            await _channel.DisposeAsync();
        }

        logger.LogInformation("Consumer stopped: queue {Queue}", queue.Queue);
    }

    private async Task OnMessageAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var eventId = ea.BasicProperties.MessageId;
        var eventType = ea.BasicProperties.Type ?? string.Empty;
        var correlationId = ReadHeader(ea, "x-correlation-id");

        // бізнес-контекст автоматично потрапляє в усі логи обробки (Serilog / OpenTelemetry)
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["EventId"] = eventId,
            ["EventType"] = eventType,
            ["CorrelationId"] = correlationId,
            ["Queue"] = queue.Queue
        });

        // відновлюємо контекст із заголовків: Consumer-span стає нащадком publish-span з Каталогу
        using var activity = TracePropagation.StartConsumerActivity(ea, queue.Queue, correlationId);

        logger.LogInformation("Received {EventType} {EventId} from {Queue}", eventType, eventId, queue.Queue);

        try
        {
            await HandleAsync(eventType, ea.Body, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // зупинка застосунку: без підтвердження, брокер перепостачить повідомлення після перезапуску
            logger.LogWarning("Processing of {EventType} {EventId} interrupted by shutdown, it will be redelivered",
                eventType, eventId);
            return;
        }
        catch (Exception ex) when (ex is JsonException or PermanentFailureException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            // постійний збій: повтор не допоможе, повідомлення йде в DLQ
            logger.LogError(ex, "Permanent failure for {EventType} {EventId}, sending to DLQ {Dlq}",
                eventType, eventId, queue.DeadLetterQueue);
            await Channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            await RetryOrDeadLetterAsync(ea, ex);
            return;
        }

        // успіх: підтверджуємо (повідомлення видаляється з черги)
        await Channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        logger.LogInformation("Acknowledged {EventType} {EventId}", eventType, eventId);
    }

    /// <summary>
    /// Тимчасовий збій. Повтори з лічильником x-retry-count додаємо в Частині 5.1;
    /// поки що повідомлення з будь-яким збоєм іде в DLQ.
    /// </summary>
    protected virtual async Task RetryOrDeadLetterAsync(BasicDeliverEventArgs ea, Exception ex)
    {
        logger.LogError(ex, "Failed to process {EventType} {EventId}, sending to DLQ {Dlq}",
            ea.BasicProperties.Type, ea.BasicProperties.MessageId, queue.DeadLetterQueue);
        await Channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
    }

    /// <summary>Значення заголовка повідомлення (рядки в AMQP приходять як byte[]).</summary>
    protected static string? ReadHeader(BasicDeliverEventArgs ea, string name)
    {
        if (ea.BasicProperties.Headers is null || !ea.BasicProperties.Headers.TryGetValue(name, out var value))
            return null;

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string s => s,
            null => null,
            _ => value.ToString()
        };
    }
}