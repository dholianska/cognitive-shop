using Orders.Dal.Abstractions;
using Orders.Domain.Exceptions;
using Platform.EventBus;

namespace Orders.Api.IntegrationEvents;

/// <summary>
/// Ідемпотентний обробник події: EventId фіксується в ProcessedEvents у тій самій транзакції,
/// що й бізнес-зміна. Повторна доставка тієї самої події нічого не змінює вдруге.
/// </summary>
public abstract class IdempotentEventHandler<TEvent>(IUnitOfWork uow, ILogger logger)
    where TEvent : IntegrationEvent
{
    protected IUnitOfWork Uow => uow;
    protected ILogger Logger => logger;

    protected abstract long ProductIdOf(TEvent e);

    /// <summary>Перевіряє дані події; некоректні дані це постійний збій (DLQ).</summary>
    protected abstract void Validate(TEvent e);

    /// <summary>Застосовує зміну до локальної копії. true, якщо зміну застосовано (не застаріла подія).</summary>
    protected abstract Task<bool> ApplyAsync(TEvent e, CancellationToken ct);

    protected static void Require(bool condition, string message)
    {
        if (!condition) throw new PermanentFailureException(message);
    }

    public async Task HandleAsync(TEvent e, CancellationToken ct)
    {
        Validate(e);

        await Uow.BeginTransactionAsync(ct: ct);
        try
        {
            // 1) ідемпотентність: подію вже оброблено?
            if (!await Uow.ProcessedEvents.TryAddAsync(e.EventId, ct))
            {
                Logger.LogInformation(
                    "Duplicate {EventType} {EventId} for product {ProductId} skipped",
                    typeof(TEvent).Name, e.EventId, ProductIdOf(e));
                await Uow.RollbackAsync(ct);
                return;
            }

            // 2) бізнес-зміна в тій самій транзакції
            var applied = await ApplyAsync(e, ct);
            await Uow.CommitAsync(ct);

            Logger.LogInformation(
                "Handled {EventType} {EventId} for product {ProductId}: {Result}",
                typeof(TEvent).Name, e.EventId, ProductIdOf(e),
                applied ? "snapshot updated" : "stale event ignored");
        }
        catch (Exception ex) when (ex is ValidationException or BusinessConflictException)
        {
            Logger.LogWarning(ex,
                "Handling {EventType} {EventId} failed for product {ProductId}: business rules violated",
                typeof(TEvent).Name, e.EventId, ProductIdOf(e));

            await Uow.RollbackAsync(ct);
            throw new PermanentFailureException(
                $"{typeof(TEvent).Name} {e.EventId} violates business rules: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex,
                "Handling {EventType} {EventId} failed for product {ProductId}: transient failure",
                typeof(TEvent).Name, e.EventId, ProductIdOf(e));

            await Uow.RollbackAsync(ct);
            throw;   // тимчасовий збій (БД недоступна, таймаут): consumer вирішить, що робити
        }
    }
}