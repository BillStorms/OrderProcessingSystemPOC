using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Models;
using OrderService.Application.Interfaces;
using OrderService.Infrastructure.Data;

namespace OrderService.Infrastructure.Repositories;

public class SqlOrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;

    public SqlOrderRepository(OrderDbContext context)
    {
        _context = context;
    }

    public async Task InsertAsync(Order order)
    {
        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();
    }

    public async Task<Order?> GetAsync(string orderId)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);
    }

    public async Task UpdateAsync(Order order)
    {
        _context.Orders.Update(order);
        await _context.SaveChangesAsync();
    }
}
