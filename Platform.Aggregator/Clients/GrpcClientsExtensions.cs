using Catalog.Grpc;
using Orders.Grpc;
using Reviews.Grpc;

namespace Platform.Aggregator.Clients;

public static class GrpcClientsExtensions
{
    public static IServiceCollection AddGrpcClients(this IServiceCollection services)
    {
        // https: gRPC потребує HTTP/2, який на HTTPS-ендпоінті узгоджується через ALPN
        services.AddGrpcClient<CatalogService.CatalogServiceClient>(o => o.Address = new Uri("https://catalog-api"));
        services.AddGrpcClient<OrdersService.OrdersServiceClient>(o => o.Address = new Uri("https://orders-api"));
        services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o => o.Address = new Uri("https://reviews-api"));
        return services;
    }
}