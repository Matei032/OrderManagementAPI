using AutoMapper;
using System;
using System.Globalization;
using OrderManagementAPI.Features.Orders;
using OrderManagementAPI.Features.Orders.DTOs;

namespace OrderManagementAPI.Common.Mapping;

public class AdvancedOrderMappingProfile : Profile
{
    public AdvancedOrderMappingProfile()
    {
        CreateMap<CreateOrderProfileRequest, Order>()
            .ForMember(d => d.Id, o => o.MapFrom(_ => Guid.NewGuid()))
            .ForMember(d => d.CreatedAt, o => o.MapFrom(_ => DateTime.UtcNow))
            .ForMember(d => d.IsAvailable, o => o.MapFrom(s => s.StockQuantity > 0))
            .ForMember(d => d.UpdatedAt, o => o.Ignore())
            .ForMember(d => d.Price, o => o.MapFrom(s =>
                s.Category == OrderCategory.Children ? Math.Round(s.Price * 0.9m, 2) : s.Price))
            .ForMember(d => d.CoverImageUrl, o => o.MapFrom(s =>
                s.Category == OrderCategory.Children ? null : s.CoverImageUrl));

        CreateMap<Order, OrderProfileDto>()
            .ForMember(d => d.CategoryDisplayName, o => o.MapFrom<CategoryDisplayResolver>())
            .ForMember(d => d.FormattedPrice, o => o.MapFrom<PriceFormatterResolver>())
            .ForMember(d => d.PublishedAge, o => o.MapFrom<PublishedAgeResolver>())
            .ForMember(d => d.AuthorInitials, o => o.MapFrom<AuthorInitialsResolver>())
            .ForMember(d => d.AvailabilityStatus, o => o.MapFrom<AvailabilityStatusResolver>());
    }
}

public class CategoryDisplayResolver : IValueResolver<Order, OrderProfileDto, string>
{
    public string Resolve(Order src, OrderProfileDto _, string __, ResolutionContext ___) => src.Category switch
    {
        OrderCategory.Fiction => "Fiction & Literature",
        OrderCategory.NonFiction => "Non-Fiction",
        OrderCategory.Technical => "Technical & Professional",
        OrderCategory.Children => "Children's Orders",
        _ => "Uncategorized"
    };
}

public class PriceFormatterResolver : IValueResolver<Order, OrderProfileDto, string>
{
    public string Resolve(Order src, OrderProfileDto _, string __, ResolutionContext ___)
        => src.Price.ToString("C2", CultureInfo.CurrentCulture);
}

public class PublishedAgeResolver : IValueResolver<Order, OrderProfileDto, string>
{
    public string Resolve(Order src, OrderProfileDto _, string __, ResolutionContext ___)
    {
        var days = (DateTime.UtcNow.Date - src.PublishedDate.Date).TotalDays;
        if (days < 30) return "New Release";
        if (days < 365) return $"{(int)(days / 30)} months old";
        if (days < 1825) return $"{(int)(days / 365)} years old";
        if (Math.Abs(days - 1825) < 0.5) return "Classic";
        return $"{(int)(days / 365)} years old";
    }
}

public class AuthorInitialsResolver : IValueResolver<Order, OrderProfileDto, string>
{
    public string Resolve(Order src, OrderProfileDto _, string __, ResolutionContext ___)
    {
        if (string.IsNullOrWhiteSpace(src.Author)) return "?";
        var parts = src.Author.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return char.ToUpperInvariant(parts[0][0]).ToString();
        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }
}

public class AvailabilityStatusResolver : IValueResolver<Order, OrderProfileDto, string>
{
    public string Resolve(Order src, OrderProfileDto _, string __, ResolutionContext ___)
    {
        if (!src.IsAvailable) return "Out of Stock";
        if (src.StockQuantity <= 0) return "Unavailable";
        if (src.StockQuantity == 1) return "Last Copy";
        if (src.StockQuantity <= 5) return "Limited Stock";
        return "In Stock";
    }
}
