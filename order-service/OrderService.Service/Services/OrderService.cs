using OrderService.Domain.Models;
using OrderService.Service.Interfaces;
using OrderService.Service.Events;
using OrderService.Service.Models;
using OrderService.Service.Validators;
using OrderService.Service.DTOs;
using Microsoft.Extensions.Logging;

namespace OrderService.Service.Services;

public class OrderManagementService : IOrderService
{
    private readonly IOrderRepository _repo;
    private readonly IEventPublisher _publisher;
    private readonly OrderValidator _validator;
    private readonly ILogger<OrderManagementService> _logger;

    public OrderManagementService(
      IOrderRepository repo,
      IEventPublisher publisher,
      ILogger<OrderManagementService> logger)
    {
        _repo = repo;
        _publisher = publisher;
        _validator = new OrderValidator();
        _logger = logger;
    }

    public async Task<string> CreateOrderAsync(CreateOrderRequest request)
    {
        _validator.Validate(request);

        var correlationId = Guid.NewGuid().ToString();

        _logger.LogInformation("Creating order for customer {CustomerId}, CorrelationId: {CorrelationId}",
          request.CustomerId, correlationId);

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

        _logger.LogInformation("Order {OrderId} created successfully, CorrelationId: {CorrelationId}",
          order.OrderId, correlationId);

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
                CorrelationId = correlationId
            }
        };

        await _publisher.PublishOrderCreatedAsync(evt);

        _logger.LogInformation("Published OrderCreatedEvent for Order {OrderId}, CorrelationId: {CorrelationId}",
          order.OrderId, correlationId);

        return order.OrderId;
    }

    public Task<Order?> GetOrderAsync(string orderId)
    {
        _logger.LogInformation("Retrieving order {OrderId}", orderId);
        return _repo.GetAsync(orderId);
    }

    public async Task<bool> UpdateOrderStatusAsync(string orderId, OrderStatus status, FulfillmentDetails? fulfillment = null)
    {
        _logger.LogInformation("Updating order {OrderId} to status {Status}", orderId, status);

        var order = await _repo.GetAsync(orderId);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for status update", orderId);
            return false;
        }

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        if (fulfillment != null)
        {
            order.Fulfillment = fulfillment;
        }

        await _repo.UpdateAsync(order);

        _logger.LogInformation("Order {OrderId} updated to status {Status}", orderId, status);
        return true;
    }
}
