namespace OrderService.Application.Events;

public class EventMetadata
{
  public string Source { get; set; } = "OrderService";
  public string CorrelationId { get; set; } = null!;
}