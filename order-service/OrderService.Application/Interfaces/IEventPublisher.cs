using OrderService.Application.Events;

namespace OrderService.Application.Interfaces;

public interface IEventPublisher
{
    Task PublishOrderCreatedAsync(OrderCreatedEvent evt);
}

