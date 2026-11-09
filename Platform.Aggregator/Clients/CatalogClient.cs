using System.Net;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

public class CatalogClient(HttpClient http, ILogger<CatalogClient> logger)
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        IReadOnlyCollection<long> productIds, CancellationToken ct)
    {
        var products = await Task.WhenAll(productIds.Select(id => GetProductAsync(id, ct)));
        return products.Where(p => p is not null).Select(p => p!).ToList();
    }

    private async Task<ProductDto?> GetProductAsync(long id, CancellationToken ct)
    {
        logger.LogInformation("Calling catalog-api for product {ProductId}", id);

        using var response = await http.GetAsync($"/api/products/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>(ct);
    }
}