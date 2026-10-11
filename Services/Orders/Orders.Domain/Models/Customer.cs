namespace Orders.Domain.Models;

public class Customer
{
    public long Id { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
}