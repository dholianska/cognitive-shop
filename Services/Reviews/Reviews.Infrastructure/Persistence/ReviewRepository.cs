using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Infrastructure.Persistence;

/// <summary>–еал≥зац≥€ доменного ≥нтерфейсу на MongoDB Driver (сторона запису).</summary>
public class ReviewRepository(MongoDbContext context) : IReviewRepository
{
    private readonly IMongoCollection<Review> _reviews = context.Reviews;

    public async Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _reviews.Find(r => r.Id == id).FirstOrDefaultAsync(ct);

    public Task<bool> ExistsAsync(long productId, string authorId, CancellationToken ct = default)
        => _reviews.Find(r => r.ProductId == productId && r.Author.UserId == authorId).AnyAsync(ct);

    // дубль (productId + author.userId) в≥дхилить ун≥кальний ≥ндекс: MongoWriteException (DuplicateKey) -> 409
    public Task AddAsync(Review review, CancellationToken ct = default)
        => _reviews.InsertOneAsync(review, cancellationToken: ct);

    /// <summary>
    /// ќптим≥стична конкурентн≥сть: зам≥на застосовуЇтьс€ лише до документа з ќ„≤ ”¬јЌќё верс≥Їю.
    /// якщо хтось зм≥нив документ ран≥ше, верс≥€ вже ≥нша, зб≥г≥в 0, ≥ це конфл≥кт (409),
    /// а не мовчазне затиранн€ чужих зм≥н.
    /// </summary>
    public async Task UpdateAsync(Review review, CancellationToken ct = default)
    {
        var expectedVersion = review.Version;
        review.BumpVersion();

        var result = await _reviews.ReplaceOneAsync(
            r => r.Id == review.Id && r.Version == expectedVersion,
            review,
            cancellationToken: ct);

        if (result.MatchedCount == 0)
            throw new ConflictException(
                $"Review {review.Id} was modified by another request (or deleted); reload it and try again");
    }

    public async Task<bool> DeleteAsync(ObjectId id, CancellationToken ct = default)
    {
        var result = await _reviews.DeleteOneAsync(r => r.Id == id, ct);
        return result.DeletedCount > 0;
    }
}