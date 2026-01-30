namespace OrderService.Domain.Models;

public enum OrderStatus
{
    Created,
    Pending,
    Processing,
    Shipped,
    Failed
}
