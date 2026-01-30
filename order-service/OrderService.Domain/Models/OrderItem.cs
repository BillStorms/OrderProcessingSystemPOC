namespace OrderService.Domain.Models;

public class OrderItem
{
    public string ProductId { get; set; } = null!;
    public int Quantity { get; set; }
}
