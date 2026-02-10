namespace OrderService.Api.DTOs;

public class UpdateOrderStatusRequest
{
    public string Status { get; set; } = null!;
    public string? TrackingNumber { get; set; }
    public string? Carrier { get; set; }
    public DateTime? ShippedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
