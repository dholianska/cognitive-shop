using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.EventBus;

/// <summary>Постійна (непоправна) помилка: повторна спроба не допоможе, повідомлення йде в DLQ.</summary>
public sealed class PermanentFailureException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Базовий споживач черги: BackgroundService з ручним підтвердженням (autoAck: false).
/// Успіх → Ack; тимчасовий збій → повтор із лічильником x-retry-count;
/// постійний збій або вичерпані спроби → Nack без requeue (DLX → DLQ).
/// Конкретний сервіс реалізує лише HandleAsync.
/// </summary>
public abstract class RabbitMqConsumer(IConnection connection, ConsumerQueue queue, ILogger logger)
    : BackgroundService
{
    private const string RetryHeader = "x-retry-count";

    private IChannel? _channel;

    protected ILogger Logger => logger;
    protected ConsumerQueue Queue => queue;
    protected IChannel Channel => _channel ?? throw new InvalidOperationException("Consumer channel is not started");

    /// <summary>Скільки непідтверджених повідомлень споживач може мати одночасно.</summary>
    protected virtual ushort PrefetchCount => 10;

    /// <summary>Максимум повторів тимчасового збою; після нього повідомлення йде в DLQ.</summary>
    protected virtual int MaxRetries => 3;

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

        logger.LogInformation("Consumer started: queue {Queue}, prefetch {Prefetch}, max retries {MaxRetries}",
            queue.Queue, PrefetchCount, MaxRetries);
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

        // відновлюємо контекст із заголовків: Consumer-span стає нащадком publish-span
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
            // постійний збій: повтор не допоможе, повідомлення йде в DLQ
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Permanent failure for {EventType} {EventId}, sending to DLQ {Dlq}",
                eventType, eventId, queue.DeadLetterQueue);
            MessagingMetrics.Record("dead_lettered", queue.Queue);
            await Channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }
        catch (Exception ex)
        {
            // тимчасовий збій (БД недоступна, таймаут): повтор з лічильником
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            await RetryOrDeadLetterAsync(ea, ex, stoppingToken);
            return;
        }

        // успіх: підтверджуємо (повідомлення видаляється з черги)
        await Channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        MessagingMetrics.Record("handled", queue.Queue);
        logger.LogInformation("Acknowledged {EventType} {EventId}", eventType, eventId);
    }

    /// <summary>
    /// Тимчасовий збій. Лічильник спроб у заголовку x-retry-count; повторне повідомлення публікується
    /// заново (requeue не змінює заголовків), оригінал підтверджується. Після MaxRetries: Nack без requeue → DLQ.
    /// </summary>
    protected virtual async Task RetryOrDeadLetterAsync(BasicDeliverEventArgs ea, Exception ex, CancellationToken ct)
    {
        var eventId = ea.BasicProperties.MessageId;
        var eventType = ea.BasicProperties.Type;
        var attempt = int.TryParse(ReadHeader(ea, RetryHeader), out var parsed) ? parsed : 0;

        if (attempt >= MaxRetries)
        {
            logger.LogError(ex, "{EventType} {EventId} failed after {Attempts} retries, sending to DLQ {Dlq}",
                eventType, eventId, attempt, queue.DeadLetterQueue);
            MessagingMetrics.Record("dead_lettered", queue.Queue);
            await Channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);   // → DLX → DLQ
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));   // 1 с, 2 с, 4 с
        logger.LogWarning(ex, "{EventType} {EventId} failed, retry {Attempt}/{MaxRetries} in {Delay} s",
            eventType, eventId, attempt + 1, MaxRetries, delay.TotalSeconds);

        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
            return;   // зупинка: повідомлення лишається непідтвердженим і перепостачиться
        }

        var headers = new Dictionary<string, object?>(ea.BasicProperties.Headers ?? new Dictionary<string, object?>())
        {
            [RetryHeader] = attempt + 1
        };
        var props = new BasicProperties(ea.BasicProperties) { Headers = headers };

        // публікуємо напряму в цю чергу (default exchange), а не через topic exchange:
        // інакше копію отримали б і інші черги, прив'язані до того самого routing key
        await Channel.BasicPublishAsync(exchange: "", routingKey: queue.Queue, mandatory: false,
            basicProperties: props, body: ea.Body.ToArray(), cancellationToken: CancellationToken.None);

        // оригінал підтверджуємо лише після того, як копію опубліковано
        await Channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        MessagingMetrics.Record("retried", queue.Queue);
    }

    /// <summary>Значення заголовка повідомлення (рядки в AMQP приходять як byte[]).</summary>
    protected static string? ReadHeader(BasicDeliverEventArgs ea, string name)
    {
        if (ea.BasicProperties.Headers is null || !ea.BasicProperties.Headers.TryGetValue(name, out var value))
            return null;

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            null => null,
            _ => value.ToString()
        };
    }
}