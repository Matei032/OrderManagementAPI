using System;

namespace OrderManagementAPI.Features.Orders;

public class Order
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Author { get; set; } = default!;
    public string ISBN { get; set; } = default!;
    public OrderCategory Category { get; set; }
    public decimal Price { get; set; }
    public DateTime PublishedDate { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool IsAvailable { get; set; }
    public int StockQuantity { get; set; } = 0;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
