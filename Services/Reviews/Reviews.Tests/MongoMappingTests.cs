using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence;
using Xunit;

namespace Reviews.Tests;

public class MongoMappingTests
{
    public MongoMappingTests() => MongoMappings.Register();

    private static Review CreateReview()
    {
        var review = new Review(42, "Ноутбук X15", new Author("u-17", "Олена"), new Rating(5), "Чудовий екран");
        review.AddComment(new Author("u-21", "Андрій"), "Згоден");
        return review;
    }

    [Fact]
    public void Review_is_stored_with_camelCase_names_and_without_dotnet_internals()
    {
        var doc = CreateReview().ToBsonDocument();

        Assert.True(doc["_id"].IsObjectId);
        Assert.Equal(42, doc["productId"].AsInt32);            // int32, як у Лаб. 01
        Assert.Equal("Ноутбук X15", doc["productName"].AsString);
        Assert.True(doc["rating"].IsInt32);                    // Value Object Rating: звичайне число
        Assert.Equal(5, doc["rating"].AsInt32);
        Assert.Equal("u-17", doc["author"]["userId"].AsString);       // вкладений документ
        Assert.Equal("Олена", doc["author"]["displayName"].AsString);
        Assert.Single(doc["comments"].AsBsonArray);                   // приватне поле -> масив comments
        Assert.True(doc.Contains("createdAt"));
        Assert.True(doc.Contains("version"));

        Assert.False(doc.Contains("_comments"));   // приватне поле .NET не витікає
        Assert.False(doc.Contains("_t"));          // дискримінатор типу не додається
        Assert.False(doc.Contains("Comments"));    // і не дублюється публічною властивістю
    }

    [Fact]
    public void Review_survives_round_trip_through_bson()
    {
        var original = CreateReview();

        var restored = BsonSerializer.Deserialize<Review>(original.ToBsonDocument());

        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Author, restored.Author);   // Value Object порівнюється за значенням
        Assert.Equal(5, restored.Rating.Value);
        Assert.Equal(42, restored.ProductId);
        Assert.Single(restored.Comments);
        Assert.Equal("Згоден", restored.Comments.First().Text);
    }
}