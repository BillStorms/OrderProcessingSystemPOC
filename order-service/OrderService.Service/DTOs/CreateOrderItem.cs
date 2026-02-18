using System.ComponentModel.DataAnnotations;

namespace OrderService.Service.DTOs;

public class CreateOrderItem
{
    [Required(ErrorMessage = "ProductId is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "ProductId must be between 1 and 100 characters")]
    public string ProductId { get; set; } = null!;
    
    [Range(1, 10000, ErrorMessage = "Quantity must be between 1 and 10000")]
    public int Quantity { get; set; }
}
