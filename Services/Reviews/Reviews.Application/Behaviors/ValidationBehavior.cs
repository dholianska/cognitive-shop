using FluentValidation;
using MediatR;

namespace Reviews.Application.Behaviors;

/// <summary>
/// Виконує всі валідатори FluentValidation для будь-якої команди чи запиту.
/// Усі помилки збираються разом; якщо вони є, кидається ValidationException,
/// і обробник НЕ викликається.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(new ValidationContext<TRequest>(request), ct);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);   // обробник не викличеться

        return await next(ct);
    }
}