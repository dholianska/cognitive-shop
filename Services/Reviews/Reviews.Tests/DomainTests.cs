using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;
using Xunit;

namespace Reviews.Tests;

public class DomainTests
{
    [Fact]
    public void Rating_above_five_throws_DomainException()
        => Assert.Throws<DomainException>(() => new Rating(10));

    [Fact]
    public void Authors_with_same_values_are_equal()
        => Assert.Equal(new Author("u-1", "Олена"), new Author("u-1", "Олена"));

    [Fact]
    public void AddComment_increases_comments_count()
    {
        var review = new Review(42, "Ноутбук", new Author("u-1", "Олена"), new Rating(5), "Чудово");
        review.AddComment(new Author("u-2", "Андрій"), "Згоден");
        Assert.Single(review.Comments);
    }
}