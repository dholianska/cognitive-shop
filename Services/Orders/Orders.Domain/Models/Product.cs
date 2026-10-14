namespace Orders.Domain.Models;

/// <summary>
/// Локальна копія довідника з БД Каталогу (власник даних - Каталог).
/// Id збігається з Id в Каталозі.
/// </summary>
public class Product
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
}