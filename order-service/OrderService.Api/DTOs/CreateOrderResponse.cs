namespace OrderService.Api.DTOs;

public class CreateOrderResponse
{
    public string OrderId { get; set; } = null!;
    public string Status { get; set; } = null!;
}
