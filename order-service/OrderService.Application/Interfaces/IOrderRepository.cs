using OrderService.Domain.Models;

namespace OrderService.Application.Interfaces;

public interface IOrderRepository
{
    Task InsertAsync(Order order);
    Task<Order?> GetAsync(string orderId);
    Task UpdateAsync(Order order);
}
