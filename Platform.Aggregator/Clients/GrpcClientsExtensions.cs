using Catalog.Grpc;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Orders.Grpc;
using Reviews.Grpc;

namespace Platform.Aggregator.Clients;

public static class GrpcClientsExtensions
{
    public static IServiceCollection AddGrpcClients(this IServiceCollection services)
    {
        // Критичні: Каталог (до 4 спроб) і Замовлення (до 3)
        services.AddGrpcClient<CatalogService.CatalogServiceClient>(o => o.Address = new Uri("https://catalog-api"))
            .ConfigureChannel(c => c.ServiceConfig = RetryConfig(maxAttempts: 4, initialBackoff: 0.5, maxBackoff: 4))
            .AddResilience("catalog-api-grpc", ResiliencePolicies.Critical.ForGrpc());

        services.AddGrpcClient<OrdersService.OrdersServiceClient>(o => o.Address = new Uri("https://orders-api"))
            .ConfigureChannel(c => c.ServiceConfig = RetryConfig(maxAttempts: 3, initialBackoff: 0.5, maxBackoff: 3))
            .AddResilience("orders-api-grpc", ResiliencePolicies.Critical.ForGrpc());

        // Некритичний: Відгуки (до 2 спроб, швидко здаємось)
        services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o => o.Address = new Uri("https://reviews-api"))
            .ConfigureChannel(c => c.ServiceConfig = RetryConfig(maxAttempts: 2, initialBackoff: 0.25, maxBackoff: 1))
            .AddResilience("reviews-api-grpc", ResiliencePolicies.NonCritical.ForGrpc());

        return services;
    }

    /// <summary>Retry за gRPC-статусами: лише для тимчасових збоїв (Unavailable, ResourceExhausted).</summary>
    private static ServiceConfig RetryConfig(int maxAttempts, double initialBackoff, double maxBackoff) => new()
    {
        MethodConfigs =
        {
            new MethodConfig
            {
                Names = { MethodName.Default },
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = maxAttempts,                                   // gRPC обмежує значення до 5
                    InitialBackoff = TimeSpan.FromSeconds(initialBackoff),
                    MaxBackoff = TimeSpan.FromSeconds(maxBackoff),
                    BackoffMultiplier = 2,
                    RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.ResourceExhausted }
                }
            }
        }
    };
}