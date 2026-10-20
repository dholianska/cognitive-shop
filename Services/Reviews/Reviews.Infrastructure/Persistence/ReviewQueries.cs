using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// Сторона читання: проєкція одразу в ReviewDto через aggregation pipeline.
/// З бази вибираються лише потрібні поля (без масиву коментарів); їхню кількість рахує сама база ($size).
/// Фільтр, сортування й пагінація (skip/limit) виконуються в MongoDB, а не в пам'яті.
/// </summary>
public class ReviewQueries(MongoDbContext context) : IReviewQueries
{
    private static readonly BsonDocument Projection = new()
    {
        { "productId", 1 },
        { "productName", 1 },
        { "author.displayName", 1 },
        { "rating", 1 },
        { "text", 1 },
        { "version", 1 },
        { "commentsCount", new BsonDocument("$size",
            new BsonDocument("$ifNull", new BsonArray { "$comments", new BsonArray() })) }
    };

    public async Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return null;

        var doc = await context.ReviewDocuments.Aggregate()
            .Match(Builders<BsonDocument>.Filter.Eq("_id", objectId))
            .Project(Projection)
            .FirstOrDefaultAsync(ct);

        return doc is null ? null : ToDto(doc);
    }

    public async Task<PagedResult<ReviewDto>> GetListAsync(
        long? productId, int page, int pageSize, CancellationToken ct)
    {
        var filter = productId is null
            ? Builders<BsonDocument>.Filter.Empty
            : Builders<BsonDocument>.Filter.Eq("productId", productId.Value);

        var total = await context.ReviewDocuments.CountDocumentsAsync(filter, cancellationToken: ct);

        var docs = await context.ReviewDocuments.Aggregate()
            .Match(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("createdAt").Descending("_id"))   // стабільний порядок
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .Project(Projection)
            .ToListAsync(ct);

        return new PagedResult<ReviewDto>(docs.Select(ToDto).ToList(), total, page, pageSize);
    }

    private static ReviewDto ToDto(BsonDocument d) => new()
    {
        Id = d["_id"].AsObjectId.ToString(),                       // ObjectId -> рядок
        ProductId = d["productId"].ToInt64(),
        ProductName = d["productName"].AsString,
        AuthorName = d["author"]["displayName"].AsString,
        Rating = d["rating"].ToInt32(),
        Text = d["text"].AsString,
        CommentsCount = d["commentsCount"].ToInt32(),
        Version = d.GetValue("version", 0).ToInt32()
    };
}