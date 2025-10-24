using System;

namespace OrderManagementAPI.Features.Orders.DTOs;

public class OrderProfileDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Author { get; set; } = default!;
    public string ISBN { get; set; } = default!;
    public string CategoryDisplayName { get; set; } = default!;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = default!;
    public DateTime PublishedDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool IsAvailable { get; set; }
    public int StockQuantity { get; set; }
    public string PublishedAge { get; set; } = default!;
    public string AuthorInitials { get; set; } = default!;
    public string AvailabilityStatus { get; set; } = default!;
}
