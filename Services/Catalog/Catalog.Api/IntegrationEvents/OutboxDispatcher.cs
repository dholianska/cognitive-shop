using System.Text;
using Catalog.Dal;
using Microsoft.EntityFrameworkCore;
using Platform.EventBus;

namespace Catalog.Api.IntegrationEvents;

/// <summary>
/// Фоновий процес Transactional Outbox: кожні 2 с публікує необроблені повідомлення з таблиці OutboxMessages.
/// Доставка щонайменше один раз: якщо застосунок впаде після публікації, але до позначки, повідомлення піде вдруге,
/// а споживачі це витримують (ідемпотентні за EventId).
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopes, IEventPublisher publisher, ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox dispatcher started, polling every {Interval} s", PollInterval.TotalSeconds);

        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Outbox dispatch iteration failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DispatchPendingAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishRawAsync(new OutboundMessage(
                    message.EventId, message.EventType, message.Exchange, message.RoutingKey,
                    Encoding.UTF8.GetBytes(message.Payload), message.OccurredAt, message.SchemaVersion,
                    message.CorrelationId, message.TraceParent), ct);

                message.ProcessedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);   // позначаємо по одному, щоб після збою дублікатів було якнайменше

                logger.LogInformation(
                    "Outbox message {EventType} {EventId} delivered to the broker (attempts {Attempts}, correlation {CorrelationId})",
                    message.EventType, message.EventId, message.Attempts + 1, message.CorrelationId);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

                try { await db.SaveChangesAsync(ct); }
                catch (Exception saveEx) { logger.LogWarning(saveEx, "Could not record outbox failure for {EventId}", message.EventId); }

                logger.LogError(ex,
                    "Failed to deliver outbox message {EventType} {EventId} (attempt {Attempts}, correlation {CorrelationId}), will retry",
                    message.EventType, message.EventId, message.Attempts, message.CorrelationId);

                break;   // зберігаємо порядок: наступні події не обганяють цю
            }
        }
    }
}