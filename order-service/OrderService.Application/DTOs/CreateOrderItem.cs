namespace OrderService.Application.DTOs;

public class CreateOrderItem
{
  public string ProductId { get; set; } = null!;
  public int Quantity { get; set; }
}
