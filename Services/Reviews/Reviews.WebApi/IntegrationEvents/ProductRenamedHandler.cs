using MongoDB.Bson;
using MongoDB.Driver;
using Platform.EventBus;
using Reviews.Application.Caching;
using Reviews.Infrastructure.Persistence;
using Reviews.Domain.Entities;

namespace Reviews.WebApi.IntegrationEvents;

/// <summary>Оновлює денормалізовану назву товару в усіх відгуках про нього (політика дублювання, Лаб. 01).</summary>
public sealed class ProductRenamedHandler(
    MongoDbContext db,
    ITwoLevelCache cache,
    IListVersionStore listVersions,
    ILogger<ProductRenamedHandler> logger)
{
    private IMongoCollection<BsonDocument> Processed => db.Database.GetCollection<BsonDocument>("processedEvents");
    private IMongoCollection<BsonDocument> Snapshots => db.Database.GetCollection<BsonDocument>("productSnapshots");

    public async Task HandleAsync(ProductRenamedEvent e, CancellationToken ct)
    {
        if (e.ProductId <= 0 || string.IsNullOrWhiteSpace(e.NewName))
            throw new PermanentFailureException($"ProductRenamedEvent {e.EventId} has invalid data");

        // 1) ідемпотентність: подію вже оброблено?
        var alreadyProcessed = await Processed
            .Find(Builders<BsonDocument>.Filter.Eq("_id", e.EventId.ToString()))
            .AnyAsync(ct);
        if (alreadyProcessed)
        {
            logger.LogInformation("Duplicate ProductRenamedEvent {EventId} for product {ProductId} skipped",
                e.EventId, e.ProductId);
            return;
        }

        // 2) out-of-order: чи не застосовано вже новішу подію про цей товар?
        var occurredAt = DateTime.SpecifyKind(e.OccurredAt, DateTimeKind.Utc);
        var snapshot = await Snapshots
            .Find(Builders<BsonDocument>.Filter.Eq("_id", (long)e.ProductId))
            .FirstOrDefaultAsync(ct);

        DateTime? lastApplied = snapshot is not null
            && snapshot.TryGetValue("occurredAt", out var stored) && stored.IsBsonDateTime
                ? stored.ToUniversalTime()
                : null;

        var stale = lastApplied is not null && lastApplied >= occurredAt;

        if (!stale)
        {
            // 3) застосовуємо: «встановити назву» ідемпотентне, тож безпечне при повторній доставці
            var result = await db.Reviews.UpdateManyAsync(
                r => r.ProductId == e.ProductId,
                Builders<Review>.Update.Set(r => r.ProductName, e.NewName),
                cancellationToken: ct);

            await Snapshots.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", (long)e.ProductId),
                new BsonDocument
                {
                    { "_id", (long)e.ProductId },
                    { "productName", e.NewName },
                    { "occurredAt", new BsonDateTime(occurredAt) }
                },
                new ReplaceOptions { IsUpsert = true },
                ct);

            await InvalidateCacheAsync(e.ProductId, ct);

            logger.LogInformation(
                "Product {ProductId} renamed to {NewName} in {Count} reviews (event {EventId})",
                e.ProductId, e.NewName, result.ModifiedCount, e.EventId);
        }
        else
        {
            logger.LogInformation(
                "Stale ProductRenamedEvent {EventId} for product {ProductId} ignored (applied {LastApplied:o}, event {OccurredAt:o})",
                e.EventId, e.ProductId, lastApplied, occurredAt);
        }

        // 4) позначаємо подію обробленою ОСТАННІМ кроком (див. пояснення вище)
        try
        {
            await Processed.InsertOneAsync(new BsonDocument
            {
                { "_id", e.EventId.ToString() },
                { "eventType", nameof(ProductRenamedEvent) },
                { "processedAt", new BsonDateTime(DateTime.UtcNow) }
            }, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // паралельна обробка того самого EventId: результат вже зафіксовано
        }
    }

    /// <summary>Кеш Reviews (Лаба 06): ключі окремих відгуків і версія списків. Кеш необов'язковий.</summary>
    private async Task InvalidateCacheAsync(int productId, CancellationToken ct)
    {
        try
        {
            var reviewIds = await db.Reviews
            .Find(r => r.ProductId == productId)
            .Project(r => r.Id.ToString())
            .ToListAsync(ct);

            foreach (var id in reviewIds)
                await cache.RemoveAsync(ReviewCacheKeys.Review(id), "ProductRenamed", ct);

            await listVersions.BumpAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Cache invalidation after renaming product {ProductId} failed", productId);
        }
    }
}