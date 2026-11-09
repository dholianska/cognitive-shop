using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

public class ReviewsClient(HttpClient http, ILogger<ReviewsClient> logger)
{
    public async Task<IReadOnlyList<ReviewDto>> GetForProductsAsync(
        IReadOnlyCollection<long> productIds, CancellationToken ct)
    {
        var pages = await Task.WhenAll(productIds.Select(id => GetPageAsync(id, ct)));
        return pages.SelectMany(p => p?.Items ?? Array.Empty<ReviewDto>()).ToList();
    }

    private async Task<PagedResultDto<ReviewDto>?> GetPageAsync(long productId, CancellationToken ct)
    {
        logger.LogInformation("Calling reviews-api for product {ProductId}", productId);

        return await http.GetFromJsonAsync<PagedResultDto<ReviewDto>>(
            $"/api/reviews?productId={productId}&page=1&pageSize=20", ct);
    }
}