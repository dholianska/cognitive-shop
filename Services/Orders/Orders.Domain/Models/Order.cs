namespace Orders.Domain.Models;

public class Order
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public string Status { get; set; } = OrderStatus.Submitted;
    public DateTime OrderDate { get; set; }

    // з таблиці OrderDetails (1:1); заповнюються лише при читанні замовлення з позиціями
    public string? DeliveryEmail { get; set; }
    public string? Comment { get; set; }

    // позиції замовлення (M:N через OrderItems)
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";   // знімок на момент замовлення
    public decimal UnitPrice { get; set; }          // знімок на момент замовлення
    public decimal Discount { get; set; }
    public int Units { get; set; }
}

/// <summary>Допустимі статуси замовлення (збігаються з CHECK у таблиці Orders).</summary>
public static class OrderStatus
{
    public const string Submitted = "Submitted";
    public const string AwaitingValidation = "AwaitingValidation";
    public const string StockConfirmed = "StockConfirmed";
    public const string Paid = "Paid";
    public const string Shipped = "Shipped";
    public const string Cancelled = "Cancelled";
}