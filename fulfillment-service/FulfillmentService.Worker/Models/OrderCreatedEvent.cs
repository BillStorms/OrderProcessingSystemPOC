namespace FulfillmentService.Worker.Models;

public class OrderCreatedEvent
{
    public string EventType { get; set; } = null!;
    public string OrderId { get; set; } = null!;
    public CustomerInfo Customer { get; set; } = null!;
    public List<OrderItem> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public EventMetadata Metadata { get; set; } = null!;
}

public class CustomerInfo
{
    public string CustomerId { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public class OrderItem
{
    public string ProductId { get; set; } = null!;
    public int Quantity { get; set; }
}

public class EventMetadata
{
    public string Source { get; set; } = "OrderService";
    public string CorrelationId { get; set; } = null!;
}
