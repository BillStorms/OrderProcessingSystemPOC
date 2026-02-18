using System.ComponentModel.DataAnnotations;

namespace OrderService.Service.DTOs;

public class CreateOrderRequest
{
    [Required(ErrorMessage = "CustomerId is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "CustomerId must be between 1 and 100 characters")]
    public string CustomerId { get; set; } = null!;
    
    [Required(ErrorMessage = "CustomerName is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "CustomerName must be between 1 and 200 characters")]
    public string CustomerName { get; set; } = null!;
    
    [Required(ErrorMessage = "Items are required")]
    [MinLength(1, ErrorMessage = "At least one item is required")]
    public List<CreateOrderItem> Items { get; set; } = new();
}
