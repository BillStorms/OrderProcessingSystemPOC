using OrderService.Domain.Models;
using OrderService.Service.Interfaces;

namespace OrderService.Infrastructure.Repositories;

public class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<string, Order> _orders = new();

    public Task InsertAsync(Order order)
    {
        _orders[order.OrderId] = order;
        return Task.CompletedTask;
    }

    public Task<Order?> GetAsync(string orderId)
    {
        _orders.TryGetValue(orderId, out var order);
        return Task.FromResult(order);
    }

    public Task UpdateAsync(Order order)
    {
        _orders[order.OrderId] = order;
        return Task.CompletedTask;
    }
}
