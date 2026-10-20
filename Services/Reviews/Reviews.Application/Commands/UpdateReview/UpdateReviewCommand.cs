using AutoMapper;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Commands.UpdateReview;

/// <summary>
/// Оновлення відгуку. ExpectedVersion (з заголовка If-Match) необов'язкова: якщо задана і не збігається
/// з поточною версією, повертається конфлікт. Результат: оновлений відгук з новою версією.
/// </summary>
public record UpdateReviewCommand(string Id, int Rating, string Text, int? ExpectedVersion = null)
    : ICommand<ReviewDto>;

public class UpdateReviewCommandHandler(IReviewRepository reviews, IMapper mapper)
    : IRequestHandler<UpdateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(UpdateReviewCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.Id), ct)
                     ?? throw new NotFoundException($"Review {cmd.Id} not found");

        if (cmd.ExpectedVersion is { } expected && expected != review.Version)
            throw new ConflictException(
                $"Review {cmd.Id} has version {review.Version}, but version {expected} was expected");

        review.Update(new Rating(cmd.Rating), cmd.Text);

        // репозиторій застосує зміни лише до документа з очікуваною версією і збільшить Version
        await reviews.UpdateAsync(review, ct);

        return mapper.Map<ReviewDto>(review);
    }
}