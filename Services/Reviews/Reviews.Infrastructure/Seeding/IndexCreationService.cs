using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

/// <summary>
/// Створює індекси при старті застосунку. Повторне створення наявного індексу безпечне:
/// MongoDB його не дублює.
/// </summary>
public class IndexCreationService(MongoDbContext context, ILogger<IndexCreationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var keys = Builders<Review>.IndexKeys;

        var created = await context.Reviews.Indexes.CreateManyAsync(new[]
        {
            // складений: "останні відгуки про товар" (фільтр за productId + сортування за датою)
            new CreateIndexModel<Review>(
                keys.Ascending(r => r.ProductId).Descending(r => r.CreatedAt),
                new CreateIndexOptions { Name = "ix_product_created" }),

            // текстовий: повнотекстовий пошук по тексту відгуку ("none": без стемінгу англійською,
            // бо тексти українські)
            new CreateIndexModel<Review>(
                keys.Text(r => r.Text),
                new CreateIndexOptions { Name = "ix_text", DefaultLanguage = "none" }),

            // унікальний: один відгук від автора на товар. Страховка від гонки двох паралельних
            // запитів, які обидва пройшли валідацію (дубль дасть DuplicateKey -> 409)
            new CreateIndexModel<Review>(
                keys.Ascending(r => r.ProductId).Ascending("author.userId"),
                new CreateIndexOptions { Name = "ux_product_author", Unique = true })
        }, ct);

        logger.LogInformation("MongoDB indexes are ready: {Indexes}", string.Join(", ", created));
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}