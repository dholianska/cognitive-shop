using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// Єдина точка доступу до MongoDB: створює клієнт за налаштуваннями (IOptions) і віддає колекції.
/// Реєструється як Singleton: MongoClient потокобезпечний і має власний пул з'єднань.
/// </summary>
public class MongoDbContext
{
    public const string ReviewsCollectionName = "reviews";

    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("MongoDb:ConnectionString is missing");
        if (string.IsNullOrWhiteSpace(settings.DatabaseName))
            throw new InvalidOperationException("MongoDb:DatabaseName is missing");

        var client = new MongoClient(settings.ConnectionString);
        Database = client.GetDatabase(settings.DatabaseName);
    }

    public IMongoDatabase Database { get; }

    /// <summary>Типізована колекція доменних сутностей (сторона запису).</summary>
    public IMongoCollection<Review> Reviews => Database.GetCollection<Review>(ReviewsCollectionName);

    /// <summary>Та сама колекція як "сирі" документи (сторона читання: проєкція одразу в DTO).</summary>
    public IMongoCollection<BsonDocument> ReviewDocuments => Database.GetCollection<BsonDocument>(ReviewsCollectionName);
}