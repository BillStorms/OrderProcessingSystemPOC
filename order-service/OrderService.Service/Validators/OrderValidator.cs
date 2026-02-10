using OrderService.Service.DTOs;

namespace OrderService.Service.Validators;

public class OrderValidator
{
    public void Validate(CreateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
            throw new ArgumentException("CustomerId is required");

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw new ArgumentException("CustomerName is required");

        if (request.Items == null || !request.Items.Any())
            throw new ArgumentException("At least one item is required");

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductId))
                throw new ArgumentException("ProductId is required for all items");

            if (item.Quantity <= 0)
                throw new ArgumentException("Quantity must be greater than 0");
        }
    }
}
