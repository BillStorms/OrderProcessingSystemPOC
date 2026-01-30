using OrderService.Domain.Models;
using OrderService.Application.DTOs;

namespace OrderService.Application.Interfaces;

public interface IOrderService
{
    Task<string> CreateOrderAsync(CreateOrderRequest request);
    Task<Order?> GetOrderAsync(string orderId);
}
