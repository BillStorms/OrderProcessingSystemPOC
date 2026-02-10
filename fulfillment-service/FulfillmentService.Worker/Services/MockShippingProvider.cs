using Microsoft.Extensions.Configuration;
using FulfillmentService.Worker.Models;

namespace FulfillmentService.Worker.Services;

public class MockShippingProvider : IShippingProvider
{
    private readonly IConfiguration _config;
    private readonly ILogger<MockShippingProvider> _logger;
    private readonly Random _random = new();

    public MockShippingProvider(IConfiguration config, ILogger<MockShippingProvider> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<ShippingResult> ProcessShipmentAsync(string orderId, string correlationId)
    {
        _logger.LogInformation("Processing shipment for Order {OrderId}, CorrelationId: {CorrelationId}", 
            orderId, correlationId);

        // Get configuration or use defaults
        var minDelaySeconds = _config.GetValue<int>("Shipping:MinDelaySeconds", 2);
        var maxDelaySeconds = _config.GetValue<int>("Shipping:MaxDelaySeconds", 10);
        var failureRate = _config.GetValue<double>("Shipping:FailureRate", 0.1); // 10% failure rate by default

        // Simulate processing delay
        var delay = _random.Next(minDelaySeconds * 1000, maxDelaySeconds * 1000);
        _logger.LogInformation("Simulating shipping delay of {Delay}ms for Order {OrderId}", delay, orderId);
        await Task.Delay(delay);

        // Simulate random failures
        if (_random.NextDouble() < failureRate)
        {
            _logger.LogWarning("Shipping failed for Order {OrderId}", orderId);
            return new ShippingResult
            {
                Success = false,
                ErrorMessage = "Shipping provider temporarily unavailable"
            };
        }

        // Success case
        var trackingNumber = $"TRACK-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        var carriers = new[] { "FedEx", "UPS", "USPS", "DHL" };
        var carrier = carriers[_random.Next(carriers.Length)];

        _logger.LogInformation("Shipment successful for Order {OrderId}. Tracking: {TrackingNumber}, Carrier: {Carrier}", 
            orderId, trackingNumber, carrier);

        return new ShippingResult
        {
            Success = true,
            TrackingNumber = trackingNumber,
            Carrier = carrier,
            ShippedAt = DateTime.UtcNow
        };
    }
}
