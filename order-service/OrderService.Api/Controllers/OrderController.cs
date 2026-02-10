using Microsoft.AspNetCore.Mvc;
using OrderService.Service.Interfaces;
using OrderService.Service.DTOs;
using OrderService.Api.DTOs;
using OrderService.Domain.Models;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _service;

    public OrderController(IOrderService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<CreateOrderResponse>> CreateOrder(CreateOrderRequest request)
    {
        var orderId = await _service.CreateOrderAsync(request);

        return Ok(new CreateOrderResponse
        {
            OrderId = orderId,
            Status = "Created"
        });
    }

    [HttpGet("{orderId}")]
    public async Task<ActionResult<OrderStatusResponse>> GetOrder(string orderId)
    {
        var order = await _service.GetOrderAsync(orderId);

        if (order == null)
            return NotFound();

        return Ok(new OrderStatusResponse
        {
            OrderId = order.OrderId,
            Status = order.Status.ToString(),
            Fulfillment = order.Fulfillment == null
            ? null
            : new FulfillmentDetailsDto
            {
                TrackingNumber = order.Fulfillment.TrackingNumber,
                Carrier = order.Fulfillment.Carrier,
                ShippedAt = order.Fulfillment.ShippedAt,
                ErrorMessage = order.Fulfillment.ErrorMessage
            }
        });
    }

    [HttpPatch("{orderId}/status")]
    public async Task<IActionResult> UpdateOrderStatus(string orderId, [FromBody] UpdateOrderStatusRequest request)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, out var status))
            return BadRequest(new { Error = "Invalid status value" });

        FulfillmentDetails? fulfillment = null;
        if (!string.IsNullOrEmpty(request.TrackingNumber) || !string.IsNullOrEmpty(request.Carrier))
        {
            fulfillment = new FulfillmentDetails
            {
                TrackingNumber = request.TrackingNumber,
                Carrier = request.Carrier,
                ShippedAt = request.ShippedAt,
                ErrorMessage = request.ErrorMessage
            };
        }

        var success = await _service.UpdateOrderStatusAsync(orderId, status, fulfillment);

        if (!success)
            return NotFound();

        return NoContent();
    }
}
