using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Application.Caching;

namespace Reviews.Application.Behaviors;

public class CachingBehavior<TRequest, TResponse>(
    ITwoLevelCache cache,
    IListVersionStore versions,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, ICacheableQuery
    where TResponse : class
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var key = request.CacheKey;

        if (request.VersionedList)
        {
            try
            {
                key = $"{key}:v{await versions.GetAsync(ct)}";
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Redis unavailable, cache bypassed for {Key}", key);
                return await next(ct);
            }
        }

        var result = await cache.GetOrCreateAsync<TResponse>(
            key, request.Policy, async c => await next(c), ct);

        return result!;
    }
}