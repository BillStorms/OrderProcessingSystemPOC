using OrderService.Service.Events;

namespace OrderService.Service.Interfaces;

public interface IEventPublisher
{
    Task PublishOrderCreatedAsync(OrderCreatedEvent evt);
}

