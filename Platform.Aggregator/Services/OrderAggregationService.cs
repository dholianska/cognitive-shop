using System.Diagnostics;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

public class UpstreamUnavailableException(string service, Exception inner)
    : Exception($"{service} is unavailable", inner)
{
    public string Service { get; } = service;
}

public class OrderAggregationService(
    OrdersClient orders,
    CatalogClient catalog,
    ReviewsClient reviews,
    ILogger<OrderAggregationService> logger)
{
    public async Task<FullOrderDto?> GetFullOrderAsync(long orderId, CancellationToken ct)
    {
        var warnings = new List<string>();

        // Без замовлення агрегувати нічого
        OrderDto? order;
        try
        {
            order = await orders.GetOrderAsync(orderId, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "orders-api is unavailable for order {OrderId}", orderId);
            throw new UpstreamUnavailableException("orders-api", ex);
        }

        if (order is null) return null;

        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();

        // Незалежні виклики стартують одночасно
        var productsTask = SafeAsync(c => catalog.GetProductsAsync(productIds, c), "catalog-api", warnings, ct);
        var reviewsTask = SafeAsync(c => reviews.GetForProductsAsync(productIds, c), "reviews-api", warnings, ct);

        await Task.WhenAll(productsTask, reviewsTask);   // паралельно, без .Result / WaitAll

        var products = await productsTask;
        var reviewList = await reviewsTask;

        // Перевірка узгодженості: у відповіді лише товари й відгуки цього замовлення
        var idSet = productIds.ToHashSet();
        var finalProducts = (products ?? []).Where(p => idSet.Contains(p.Id)).ToList();
        var finalReviews = (reviewList ?? []).Where(r => idSet.Contains(r.ProductId)).ToList();

        if (products is not null)
        {
            var missing = productIds.Where(id => finalProducts.All(p => p.Id != id)).ToList();
            if (missing.Count > 0)
                warnings.Add($"catalog-api: products not found: {string.Join(", ", missing)}");
        }

        return new FullOrderDto(order, finalProducts, finalReviews, warnings);
    }

    private async Task<T?> SafeAsync<T>(
        Func<CancellationToken, Task<T?>> call, string service, List<string> warnings, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call(ct);
            logger.LogInformation("{Service} responded in {Elapsed} ms", service, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Service} is unavailable after {Elapsed} ms", service, sw.ElapsedMilliseconds);
            lock (warnings) warnings.Add($"{service} is unavailable");
            return default;
        }
    }
}