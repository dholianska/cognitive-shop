using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

public class ReviewSeeder(MongoDbContext context, ILogger<ReviewSeeder> logger) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        // ідемпотентність: якщо відгуки вже є, повторний запуск нічого не додає
        if (await context.Reviews.Find(FilterDefinition<Review>.Empty).AnyAsync(ct))
        {
            logger.LogInformation("Reviews collection already contains data, seeding skipped");
            return;
        }

        var first = new Review(1, "Тренажер уваги: тест Струпа", new Author("u-1", "Олена"), new Rating(5),
            "Чудовий тренажер, помітно покращилась концентрація");
        first.AddComment(new Author("u-2", "Андрій"), "Згоден, користуюсь другий тиждень");

        var reviews = new[]
        {
            first,
            new Review(2, "N-back: тренування робочої пам'яті", new Author("u-2", "Андрій"), new Rating(4),
                "Добре для робочої пам'яті, але хотілося б більше рівнів"),
            new Review(1, "Тренажер уваги: тест Струпа", new Author("u-3", "Марія"), new Rating(3),
                "Нормально, але одноманітно"),
            new Review(3, "Тест часу реакції", new Author("u-1", "Олена"), new Rating(5),
                "Швидкий тест, зручно проходити з телефона")
        };

        await context.Reviews.InsertManyAsync(reviews, cancellationToken: ct);   // bulk-вставка
        logger.LogInformation("Seeded {Count} reviews", reviews.Length);
    }
}