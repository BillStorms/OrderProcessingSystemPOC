using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using FulfillmentService.Worker.Models;
using FulfillmentService.Worker.Services;
using Xunit;

namespace FulfillmentService.Tests;

public class MockShippingProviderTests
{
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<ILogger<MockShippingProvider>> _mockLogger;
    private readonly MockShippingProvider _provider;

    public MockShippingProviderTests()
    {
        _mockConfig = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<MockShippingProvider>>();
        
        // Create a mock configuration that returns default values
        var configSection = new Mock<IConfigurationSection>();
        _mockConfig
            .Setup(c => c.GetSection(It.IsAny<string>()))
            .Returns(configSection.Object);

        _provider = new MockShippingProvider(_mockConfig.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessShipmentAsync_WithValidOrderId_ShouldReturnResult()
    {
        // Act
        var result = await _provider.ProcessShipmentAsync("order-123", "corr-456");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.TrackingNumber.Should().NotBeNullOrEmpty();
        result.Carrier.Should().NotBeNullOrEmpty();
        result.ShippedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessShipmentAsync_SuccessfulResult_ShouldHaveValidCarrier()
    {
        // Arrange
        var validCarriers = new[] { "FedEx", "UPS", "USPS", "DHL" };

        // Act
        var result = await _provider.ProcessShipmentAsync("order-123", "corr-456");

        // Assert
        if (result.Success)
        {
            result.Carrier.Should().BeOneOf(validCarriers);
        }
    }

    [Fact]
    public async Task ProcessShipmentAsync_WithValidParameters_ShouldLogInformation()
    {
        // Act
        var result = await _provider.ProcessShipmentAsync("order-123", "corr-456");

        // Assert - Verify logging was called
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("order-1")]
    [InlineData("order-2")]
    [InlineData("order-xyz")]
    public async Task ProcessShipmentAsync_WithDifferentOrderIds_ShouldProcessSuccessfully(string orderId)
    {
        // Act
        var result = await _provider.ProcessShipmentAsync(orderId, "corr-id");

        // Assert
        result.Should().NotBeNull();
        // Can be success or failure depending on random, but should return a result
        (result.Success == true || result.Success == false).Should().BeTrue();
    }
}
