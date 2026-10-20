using FluentValidation;
using MongoDB.Bson;
using Reviews.Application.Behaviors;
using Reviews.Application.Commands.CreateReview;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Xunit;

namespace Reviews.Tests;

public class ValidationTests
{
    /// <summary>Підміна репозиторію: база не потрібна.</summary>
    private sealed class FakeRepository(bool alreadyReviewed = false) : IReviewRepository
    {
        public Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default) => Task.FromResult<Review?>(null);
        public Task<bool> ExistsAsync(long productId, string authorId, CancellationToken ct = default) => Task.FromResult(alreadyReviewed);
        public Task AddAsync(Review review, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Review review, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> DeleteAsync(ObjectId id, CancellationToken ct = default) => Task.FromResult(false);
    }

    private static CreateReviewCommand Command(int rating = 5)
        => new(42, "Ноутбук X15", "u-17", "Олена", rating, "Чудовий екран");

    [Fact]
    public async Task Rating_ten_is_rejected_by_validator()
    {
        var result = await new CreateReviewCommandValidator(new FakeRepository()).ValidateAsync(Command(rating: 10));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Rating");
    }

    [Fact]
    public async Task Handler_is_not_called_when_validation_fails()
    {
        var behavior = new ValidationBehavior<CreateReviewCommand, string>(
            new[] { new CreateReviewCommandValidator(new FakeRepository()) });
        var handlerCalled = false;

        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            Command(rating: 10),
            _ => { handlerCalled = true; return Task.FromResult("id"); },
            CancellationToken.None));

        Assert.False(handlerCalled);   // обробник не виконувався
    }

    [Fact]
    public async Task Duplicate_review_is_rejected_by_async_rule()
    {
        var result = await new CreateReviewCommandValidator(new FakeRepository(alreadyReviewed: true))
            .ValidateAsync(Command());

        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("already reviewed"));
    }

    [Fact]
    public async Task Valid_command_reaches_handler()
    {
        var behavior = new ValidationBehavior<CreateReviewCommand, string>(
            new[] { new CreateReviewCommandValidator(new FakeRepository()) });

        var response = await behavior.Handle(Command(), _ => Task.FromResult("id"), CancellationToken.None);

        Assert.Equal("id", response);
    }
}