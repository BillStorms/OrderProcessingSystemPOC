using FluentAssertions;
using FulfillmentService.Worker.Models;
using Xunit;

namespace FulfillmentService.Tests;

public class OrderCreatedEventTests
{
    [Fact]
    public void OrderCreatedEvent_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var evt = new OrderCreatedEvent();

        // Assert
        evt.EventType.Should().BeNull();
        evt.OrderId.Should().BeNull();
        evt.Customer.Should().BeNull();
        evt.Items.Should().NotBeNull();
        evt.Items.Should().BeEmpty();
        evt.Metadata.Should().BeNull();
    }

    [Fact]
    public void OrderCreatedEvent_ShouldAllowSettingAllProperties()
    {
        // Arrange
        var evt = new OrderCreatedEvent
        {
            EventType = "OrderCreated",
            OrderId = "order-123",
            Customer = new CustomerInfo 
            { 
                CustomerId = "cust-1",
                Name = "John Doe" 
            },
            Items = new List<OrderItem> 
            { 
                new() { ProductId = "sku-1", Quantity = 2 }
            },
            CreatedAt = DateTime.UtcNow,
            Metadata = new EventMetadata 
            { 
                CorrelationId = "corr-456" 
            }
        };

        // Assert
        evt.EventType.Should().Be("OrderCreated");
        evt.OrderId.Should().Be("order-123");
        evt.Customer.Should().NotBeNull();
        evt.Customer.CustomerId.Should().Be("cust-1");
        evt.Customer.Name.Should().Be("John Doe");
        evt.Items.Should().HaveCount(1);
        evt.Items[0].ProductId.Should().Be("sku-1");
        evt.Items[0].Quantity.Should().Be(2);
        evt.Metadata.CorrelationId.Should().Be("corr-456");
    }

    [Fact]
    public void CustomerInfo_ShouldInitializeWithNullDefaults()
    {
        // Act
        var customer = new CustomerInfo();

        // Assert
        customer.CustomerId.Should().BeNull();
        customer.Name.Should().BeNull();
    }

    [Fact]
    public void OrderItem_ShouldInitializeWithNullProductId()
    {
        // Act
        var item = new OrderItem();

        // Assert
        item.ProductId.Should().BeNull();
        item.Quantity.Should().Be(0);
    }

    [Fact]
    public void OrderItem_ShouldAllowSettingQuantity()
    {
        // Act
        var item = new OrderItem { ProductId = "sku-1", Quantity = 5 };

        // Assert
        item.ProductId.Should().Be("sku-1");
        item.Quantity.Should().Be(5);
    }

    [Fact]
    public void EventMetadata_ShouldHaveDefaultSource()
    {
        // Act
        var metadata = new EventMetadata();

        // Assert
        metadata.Source.Should().Be("OrderService");
        metadata.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void EventMetadata_ShouldAllowOverridingSource()
    {
        // Act
        var metadata = new EventMetadata 
        { 
            Source = "CustomSource",
            CorrelationId = "corr-id"
        };

        // Assert
        metadata.Source.Should().Be("CustomSource");
        metadata.CorrelationId.Should().Be("corr-id");
    }

    [Fact]
    public void OrderCreatedEvent_WithMultipleItems_ShouldMaintainAllItems()
    {
        // Arrange
        var items = new List<OrderItem>
        {
            new() { ProductId = "sku-1", Quantity = 2 },
            new() { ProductId = "sku-2", Quantity = 1 },
            new() { ProductId = "sku-3", Quantity = 5 }
        };

        var evt = new OrderCreatedEvent { Items = items };

        // Assert
        evt.Items.Should().HaveCount(3);
        evt.Items[0].ProductId.Should().Be("sku-1");
        evt.Items[1].ProductId.Should().Be("sku-2");
        evt.Items[2].ProductId.Should().Be("sku-3");
    }
}
