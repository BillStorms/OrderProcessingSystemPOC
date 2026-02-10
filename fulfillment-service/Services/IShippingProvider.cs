using FulfillmentService.Worker.Models;

namespace FulfillmentService.Worker.Services;

public interface IShippingProvider
{
    Task<ShippingResult> ProcessShipmentAsync(string orderId, string correlationId);
}


