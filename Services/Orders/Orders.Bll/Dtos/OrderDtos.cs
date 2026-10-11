using System.ComponentModel.DataAnnotations;

namespace Orders.Bll.Dtos;

// ---- вихідні DTO ----
public record OrderItemDto(long ProductId, string ProductName, decimal UnitPrice, decimal Discount, int Units);

public record OrderDto(long Id, long CustomerId, string Status, DateTime OrderDate,
                       string? DeliveryEmail, string? Comment, IReadOnlyList<OrderItemDto> Items);

// ---- вхідні DTO: базова валідація атрибутами (рівень API) ----
public class CreateOrderDto
{
    [Range(1, long.MaxValue)]
    public long CustomerId { get; set; }

    [Required, EmailAddress, StringLength(256)]
    public string DeliveryEmail { get; set; } = "";

    [StringLength(1000)]
    public string? Comment { get; set; }

    [Required, MinLength(1)]
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class CreateOrderItemDto
{
    [Range(1, long.MaxValue)] public long ProductId { get; set; }
    [Range(1, 1000)] public int Units { get; set; }
}

public class ChangeOrderStatusDto
{
    [Required, StringLength(30)]
    public string Status { get; set; } = "";
}