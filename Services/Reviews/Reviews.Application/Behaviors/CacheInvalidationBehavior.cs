using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Application.Caching;

namespace Reviews.Application.Behaviors;

public class CacheInvalidationBehavior<TRequest, TResponse>(
    ITwoLevelCache cache,
    IListVersionStore versions,
    ICacheMetrics metrics,
    ILogger<CacheInvalidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, ICacheInvalidatingCommand
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await next(ct);   // запис у БД; якщо впав, кеш не чіпаємо

        if (request.ReviewId is { } id)
            await cache.RemoveAsync(ReviewCacheKeys.Review(id), request.Reason, ct);

        try
        {
            await versions.BumpAsync(ct);   // усі списки стають «старими» одразу
            metrics.Invalidated();
            logger.LogInformation("Cache invalidated {Key}, reason {Reason}", ReviewCacheKeys.ListVersion, request.Reason);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Redis unavailable, list invalidation skipped, reason {Reason}", request.Reason);
        }

        return response;
    }
}