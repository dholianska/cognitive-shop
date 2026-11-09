using System.Diagnostics;
using Catalog.Grpc;
using Grpc.Core;
using Orders.Grpc;
using Platform.Aggregator.Models;
using Reviews.Grpc;

namespace Platform.Aggregator.Services;

public class GrpcOrderAggregationService(
    OrdersService.OrdersServiceClient orders,
    CatalogService.CatalogServiceClient catalog,
    ReviewsService.ReviewsServiceClient reviews,
    ILogger<GrpcOrderAggregationService> logger)
{
    public async Task<FullOrderDto?> GetFullOrderAsync(long orderId, CancellationToken ct)
    {
        var warnings = new List<string>();
        Activity.Current?.SetTag("order.id", orderId);

        // Без замовлення агрегувати нічого
        OrderDto order;
        var sw = Stopwatch.StartNew();
        try
        {
            logger.LogInformation("Calling orders-api (gRPC) for order {OrderId}", orderId);
            var reply = await orders.GetOrderAsync(new GetOrderRequest { Id = orderId }, cancellationToken: ct);
            order = reply.ToDto();
            logger.LogInformation("orders-api responded in {Elapsed} ms", sw.ElapsedMilliseconds);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
        catch (RpcException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "orders-api is unavailable ({Status}) for order {OrderId}", ex.StatusCode, orderId);
            throw new UpstreamUnavailableException("orders-api", ex);
        }

        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var catalogIds = productIds.Where(id => id is > 0 and <= int.MaxValue).Select(id => (int)id).ToList();

        // Незалежні gRPC-виклики стартують одночасно (один HTTP/2-канал, кілька потоків)
        var productsTask = SafeAsync<IReadOnlyList<ProductDto>>(async c =>
        {
            var r = await catalog.GetProductsByIdsAsync(
                new GetProductsByIdsRequest { Ids = { catalogIds } }, cancellationToken: c);
            return r.Products.Select(p => p.ToDto()).ToList();
        }, "catalog-api", warnings, ct);

        var reviewsTask = SafeAsync<IReadOnlyList<ReviewDto>>(async c =>
        {
            var r = await reviews.GetForProductsAsync(
                new GetForProductsRequest { ProductIds = { productIds } }, cancellationToken: c);
            return r.Reviews.Select(x => x.ToDto()).ToList();
        }, "reviews-api", warnings, ct);

        await Task.WhenAll(productsTask, reviewsTask);   // без .Result / WaitAll

        var products = await productsTask;
        var reviewList = await reviewsTask;

        // Узгодженість: у відповіді лише товари й відгуки цього замовлення
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
            logger.LogInformation("Calling {Service} (gRPC)", service);
            var result = await call(ct);
            logger.LogInformation("{Service} responded in {Elapsed} ms", service, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Service} is unavailable ({Status}) after {Elapsed} ms",
                service, (ex as RpcException)?.StatusCode, sw.ElapsedMilliseconds);
            lock (warnings) warnings.Add($"{service} is unavailable");
            return default;
        }
    }
}