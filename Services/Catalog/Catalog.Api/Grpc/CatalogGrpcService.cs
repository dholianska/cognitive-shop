using Catalog.Bll.Dtos;
using Catalog.Bll.Services;
using Catalog.Domain.Exceptions;
using Catalog.Grpc;
using Grpc.Core;
using Serilog.Context;

namespace Catalog.Api.GrpcServices;

/// <summary>gRPC-адаптер над IProductService. Кеш (L1/L2) працює і для REST, і для gRPC, бо це той самий BLL.</summary>
public class CatalogGrpcService(IProductService products, ILogger<CatalogGrpcService> logger)
    : CatalogService.CatalogServiceBase
{
    private const int MaxBatch = 100;

    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        using var scope = LogContext.PushProperty("Protocol", "gRPC");

        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be positive"));

        try
        {
            var dto = await products.GetByIdAsync(request.Id, context.CancellationToken);   // з кешем
            return ToReply(dto);
        }
        catch (EntityNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<ProductsReply> GetProductsByIds(GetProductsByIdsRequest request, ServerCallContext context)
    {
        using var scope = LogContext.PushProperty("Protocol", "gRPC");

        if (request.Ids.Count is 0 or > MaxBatch)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Ids count must be 1..{MaxBatch}"));
        if (request.Ids.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Every id must be positive"));

        var reply = new ProductsReply();

        // Послідовно, а не Task.WhenAll: сервіс і його DbContext у межах одного виклику спільні,
        // а DbContext не потокобезпечний. Повторні читання все одно віддає кеш.
        foreach (var id in request.Ids.Distinct())
        {
            try
            {
                reply.Products.Add(ToReply(await products.GetByIdAsync(id, context.CancellationToken)));
            }
            catch (EntityNotFoundException)
            {
                logger.LogWarning("Product {ProductId} not found, skipped in batch", id);
            }
        }

        return reply;
    }

    private static ProductReply ToReply(ProductCardDto p)
    {
        var reply = new ProductReply
        {
            Id = p.Id,
            Name = p.Name,
            Sku = p.Sku,
            Price = (double)p.Price,
            AvailableStock = p.AvailableStock
        };
        if (p.Description is not null) reply.Description = p.Description;
        if (p.Manufacturer is not null) reply.Manufacturer = p.Manufacturer;
        reply.Categories.AddRange(p.Categories);
        return reply;
    }
}