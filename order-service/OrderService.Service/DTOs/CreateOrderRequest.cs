namespace OrderService.Service.DTOs;

public class CreateOrderRequest
{
    public string CustomerId { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public List<CreateOrderItem> Items { get; set; } = new();
}
