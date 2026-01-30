namespace OrderService.Domain.Models;

public class FulfillmentDetails
{
    public string? TrackingNumber { get; set; }
    public string? Carrier { get; set; }
    public DateTime? ShippedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
