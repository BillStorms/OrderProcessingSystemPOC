using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OrderService.Domain.Models;
using OrderService.Service.DTOs;
using OrderService.Service.Interfaces;
using OrderService.Service.Services;
using Xunit;

namespace OrderService.Tests;

public class OrderManagementServiceTests
{
    private readonly Mock<IOrderRepository> _mockRepository;
    private readonly Mock<IEventPublisher> _mockPublisher;
    private readonly Mock<ILogger<OrderManagementService>> _mockLogger;
    private readonly OrderManagementService _service;

    public OrderManagementServiceTests()
    {
        _mockRepository = new Mock<IOrderRepository>();
        _mockPublisher = new Mock<IEventPublisher>();
        _mockLogger = new Mock<ILogger<OrderManagementService>>();
        _service = new OrderManagementService(_mockRepository.Object, _mockPublisher.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_WithValidRequest_ShouldPersistAndPublish()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "cust-1",
            CustomerName = "Test User",
            Items = new List<CreateOrderItem> { new() { ProductId = "sku-1", Quantity = 1 } }
        };

        // Act
        var orderId = await _service.CreateOrderAsync(request);

        // Assert
        orderId.Should().NotBeNullOrEmpty();
        _mockRepository.Verify(r => r.InsertAsync(It.IsAny<Order>()), Times.Once);
        _mockPublisher.Verify(p => p.PublishOrderCreatedAsync(It.IsAny<OrderService.Service.Events.OrderCreatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_WithEmptyItems_ShouldThrow()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "cust-1",
            CustomerName = "Test",
            Items = new List<CreateOrderItem>()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateOrderAsync(request));
    }

    [Fact]
    public async Task CreateOrderAsync_WithNullCustomerId_ShouldThrow()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = null!,
            CustomerName = "Test",
            Items = new List<CreateOrderItem> { new() { ProductId = "sku-1", Quantity = 1 } }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateOrderAsync(request));
    }

    [Fact]
    public async Task GetOrderAsync_WithValidId_ShouldReturnOrder()
    {
        // Arrange
        var order = new Order 
        { 
            OrderId = "order-1", 
            CustomerId = "cust-1", 
            CustomerName = "Test",
            Status = OrderStatus.Created,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = new List<OrderItem>()
        };
        _mockRepository.Setup(r => r.GetAsync("order-1")).ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderAsync("order-1");

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be("order-1");
        result.CustomerId.Should().Be("cust-1");
    }

    [Fact]
    public async Task GetOrderAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAsync(It.IsAny<string>())).ReturnsAsync((Order?)null);

        // Act
        var result = await _service.GetOrderAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_WithValidData_ShouldUpdate()
    {
        // Arrange
        var order = new Order 
        { 
            OrderId = "order-1", 
            CustomerId = "cust-1", 
            CustomerName = "Test",
            Status = OrderStatus.Created,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = new List<OrderItem>()
        };
        _mockRepository.Setup(r => r.GetAsync("order-1")).ReturnsAsync(order);

        // Act
        var result = await _service.UpdateOrderStatusAsync("order-1", OrderStatus.Processing);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Order>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_WithNonExistentOrder_ShouldReturnFalse()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAsync(It.IsAny<string>())).ReturnsAsync((Order?)null);

        // Act
        var result = await _service.UpdateOrderStatusAsync("nonexistent", OrderStatus.Processing);

        // Assert
        result.Should().BeFalse();
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_WithFulfillmentDetails_ShouldUpdateFulfillment()
    {
        // Arrange
        var order = new Order 
        { 
            OrderId = "order-1", 
            CustomerId = "cust-1", 
            CustomerName = "Test",
            Status = OrderStatus.Created,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = new List<OrderItem>()
        };
        
        var fulfillment = new FulfillmentDetails 
        { 
            TrackingNumber = "TRACK-123",
            Carrier = "UPS",
            ShippedAt = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.GetAsync("order-1")).ReturnsAsync(order);

        // Act
        var result = await _service.UpdateOrderStatusAsync("order-1", OrderStatus.Shipped, fulfillment);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<Order>(o => 
            o.OrderId == "order-1" && 
            o.Status == OrderStatus.Shipped &&
            o.Fulfillment != null
        )), Times.Once);
    }
}
