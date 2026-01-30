namespace OrderService.Api.DTOs;

public class FulfillmentDetailsDto
{
    public string? TrackingNumber { get; set; }
    public string? Carrier { get; set; }
    public DateTime? ShippedAt { get; set; }
    public string? ErrorMessage { get; set; }
}