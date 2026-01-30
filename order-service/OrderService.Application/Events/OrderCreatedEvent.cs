using OrderService.Domain.Models;
using OrderService.Application.Models;

namespace OrderService.Application.Events;

public class OrderCreatedEvent
{
  public string EventType { get; set; } = "OrderCreated";
  public string OrderId { get; set; } = null!;
  public CustomerInfo Customer { get; set; } = null!;
  public List<OrderItem> Items { get; set; } = new();
  public DateTime CreatedAt { get; set; }
  public EventMetadata Metadata { get; set; } = null!;
}