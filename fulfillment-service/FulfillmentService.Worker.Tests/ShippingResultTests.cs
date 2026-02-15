using FluentAssertions;
using FulfillmentService.Worker.Models;
using Xunit;

namespace FulfillmentService.Worker.Tests;

public class ShippingResultTests
{
    [Fact]
    public void ShippingResult_SuccessfulResult_ShouldHaveTrackingAndCarrier()
    {
        // Arrange & Act
        var result = new ShippingResult
        {
            Success = true,
            TrackingNumber = "TRACK-ABC123",
            Carrier = "FedEx",
            ShippedAt = DateTime.UtcNow
        };

        // Assert
        result.Success.Should().BeTrue();
        result.TrackingNumber.Should().Be("TRACK-ABC123");
        result.Carrier.Should().Be("FedEx");
        result.ShippedAt.Should().NotBeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ShippingResult_FailedResult_ShouldHaveErrorMessage()
    {
        // Arrange & Act
        var result = new ShippingResult
        {
            Success = false,
            ErrorMessage = "Carrier unavailable"
        };

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Carrier unavailable");
        result.TrackingNumber.Should().BeNull();
        result.Carrier.Should().BeNull();
        result.ShippedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("FedEx")]
    [InlineData("UPS")]
    [InlineData("USPS")]
    [InlineData("DHL")]
    public void ShippingResult_WithValidCarrier_ShouldAcceptAllCarriers(string carrier)
    {
        // Act
        var result = new ShippingResult
        {
            Success = true,
            Carrier = carrier
        };

        // Assert
        result.Carrier.Should().Be(carrier);
    }

    [Fact]
    public void ShippingResult_WithValidTrackingNumber_ShouldStoreCorrectly()
    {
        // Act
        var result = new ShippingResult
        {
            TrackingNumber = "TRACK-XYZ789"
        };

        // Assert
        result.TrackingNumber.Should().Be("TRACK-XYZ789");
    }

    [Fact]
    public void ShippingResult_ShouldInitializeWithNullDefaults()
    {
        // Act
        var result = new ShippingResult();

        // Assert
        result.Success.Should().BeFalse();
        result.TrackingNumber.Should().BeNull();
        result.Carrier.Should().BeNull();
        result.ShippedAt.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ShippingResult_ShippedAt_ShouldHaveUtcTime()
    {
        // Arrange
        var utcNow = DateTime.UtcNow;

        // Act
        var result = new ShippingResult
        {
            Success = true,
            ShippedAt = utcNow
        };

        // Assert
        result.ShippedAt.Should().Be(utcNow);
        result.ShippedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }
}
