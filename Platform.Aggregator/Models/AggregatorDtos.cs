namespace Platform.Aggregator.Models;

// ---- Відповіді мікросервісів (лише потрібні Aggregator поля) ----
public record OrderItemDto(long ProductId, string ProductName, decimal UnitPrice, decimal Discount, int Units);

public record OrderDto(long Id, long CustomerId, string Status, DateTime OrderDate,
                       string? DeliveryEmail, string? Comment, IReadOnlyList<OrderItemDto> Items);

public record ProductDto(int Id, string Name, string Sku, decimal Price, int AvailableStock,
                         string? Description, string? Manufacturer, IReadOnlyList<string>? Categories);

public record ReviewDto(string Id, long ProductId, string ProductName, string AuthorName,
                        int Rating, string Text, int CommentsCount);

public record PagedResultDto<T>(IReadOnlyList<T> Items, long TotalCount, int Page, int PageSize);

// ---- Композитна відповідь під сценарій «повна картка замовлення» ----
public record FullOrderDto(
    OrderDto Order,
    IReadOnlyList<ProductDto> Products,
    IReadOnlyList<ReviewDto> Reviews,
    IReadOnlyList<string> Warnings);