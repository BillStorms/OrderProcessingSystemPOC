using OrderService.Domain.Models;
using OrderService.Application.Interfaces;
using OrderService.Application.Events;
using OrderService.Application.Models;
using OrderService.Application.Validators;
using OrderService.Application.DTOs;

namespace OrderService.Application.Services;

public class OrderManagementService : IOrderService
{
  private readonly IOrderRepository _repo;
  private readonly IEventPublisher _publisher;
  private readonly OrderValidator _validator;

  public OrderManagementService(IOrderRepository repo, IEventPublisher publisher)
  {
    _repo = repo;
    _publisher = publisher;
    _validator = new OrderValidator();
  }

  public async Task<string> CreateOrderAsync(CreateOrderRequest request)
  {
    _validator.Validate(request);

    var order = new Order
    {
      OrderId = Guid.NewGuid().ToString(),
      CustomerId = request.CustomerId,
      CustomerName = request.CustomerName,
      Items = request.Items.Select(i => new OrderItem
      {
        ProductId = i.ProductId,
        Quantity = i.Quantity
      }).ToList(),
      Status = OrderStatus.Created,
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = DateTime.UtcNow
    };

    await _repo.InsertAsync(order);

    var evt = new OrderCreatedEvent
    {
      OrderId = order.OrderId,
      Customer = new CustomerInfo
      {
        CustomerId = order.CustomerId,
        Name = order.CustomerName
      },
      Items = order.Items,
      CreatedAt = order.CreatedAt,
      Metadata = new EventMetadata
      {
        CorrelationId = Guid.NewGuid().ToString()
      }
    };

    await _publisher.PublishOrderCreatedAsync(evt);

    return order.OrderId;
  }

  public Task<Order?> GetOrderAsync(string orderId)
    => _repo.GetAsync(orderId);
}
