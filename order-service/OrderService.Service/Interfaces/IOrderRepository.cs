using OrderService.Domain.Models;

namespace OrderService.Service.Interfaces;

public interface IOrderRepository
{
    Task InsertAsync(Order order);
    Task<Order?> GetAsync(string orderId);
    Task UpdateAsync(Order order);
}
