using Catalog.Grpc;
using Orders.Grpc;
using Platform.Aggregator.Models;
using Reviews.Grpc;

namespace Platform.Aggregator.Services;

/// <summary>gRPC-повідомлення → агреговані DTO (ті самі, що й у Лаб. 05).</summary>
public static class GrpcMapping
{
    public static OrderDto ToDto(this OrderReply o) => new(
        o.Id, o.CustomerId, o.Status, o.OrderDate.ToDateTime(),
        o.HasDeliveryEmail ? o.DeliveryEmail : null,
        o.HasComment ? o.Comment : null,
        o.Items.Select(i => new OrderItemDto(
            i.ProductId, i.ProductName, (decimal)i.UnitPrice, (decimal)i.Discount, i.Units)).ToList());

    public static ProductDto ToDto(this ProductReply p) => new(
        p.Id, p.Name, p.Sku, (decimal)p.Price, p.AvailableStock,
        p.HasDescription ? p.Description : null,
        p.HasManufacturer ? p.Manufacturer : null,
        p.Categories.ToList());

    public static ReviewDto ToDto(this ReviewReply r) => new(
        r.Id, r.ProductId, r.ProductName, r.AuthorName, r.Rating, r.Text, r.CommentsCount);
}