using System.Net;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

public class OrdersClient(HttpClient http, ILogger<OrdersClient> logger)
{
    public async Task<OrderDto?> GetOrderAsync(long id, CancellationToken ct)
    {
        logger.LogInformation("Calling orders-api for order {OrderId}", id);

        using var response = await http.GetAsync($"/api/orders/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;                       // 404 = замовлення немає, це не «сервіс недоступний»

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>(ct);
    }
}