using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.Logging;
using OrderManagementAPI.Features.Orders;

namespace OrderManagementAPI.Validators;

public class CreateOrderProfileValidator : AbstractValidator<CreateOrderProfileRequest>
{
    private readonly IOrderReadRepository _repo;
    private readonly ILogger<CreateOrderProfileValidator> _logger;

    private static readonly string[] InappropriateWords =
        { "violent", "nsfw", "adult", "gore", "drugs", "sex" };

    private static readonly string[] TechnicalKeywords =
        { "architecture", "patterns", "algorithm", "database", "cloud",
          "c#", "dotnet", ".net", "linux", "network", "docker", "microservices" };

    public CreateOrderProfileValidator(IOrderReadRepository repo,
                                       ILogger<CreateOrderProfileValidator> logger)
    {
        _repo = repo;
        _logger = logger;

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .Length(1, 200)
            .Must(BeValidTitle).WithMessage("Title contains inappropriate content.")
            .MustAsync(BeUniqueTitleAsync).WithMessage("An order with this Title already exists for this Author.");

        RuleFor(x => x.Author)
            .NotEmpty().WithMessage("Author is required.")
            .Length(2, 100)
            .Must(BeValidAuthorName).WithMessage("Author contains invalid characters.");

        RuleFor(x => x.ISBN)
            .NotEmpty().WithMessage("ISBN is required.")
            .Must(BeValidISBN).WithMessage("ISBN must be 10 or 13 digits (hyphens allowed).")
            .MustAsync(BeUniqueISBNAsync).WithMessage("An order with this ISBN already exists.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Category is invalid.");

        RuleFor(x => x.Price)
            .GreaterThan(0m).WithMessage("Price must be greater than 0.")
            .LessThan(10_000m).WithMessage("Price must be less than 10,000.");

        RuleFor(x => x.PublishedDate)
            .Must(d => d.Date <= DateTime.UtcNow.Date).WithMessage("PublishedDate cannot be in the future.")
            .Must(d => d.Year >= 1400).WithMessage("PublishedDate cannot be before year 1400.");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("StockQuantity cannot be negative.")
            .LessThanOrEqualTo(100_000).WithMessage("StockQuantity cannot exceed 100,000.");

        When(x => !string.IsNullOrWhiteSpace(x.CoverImageUrl), () =>
        {
            RuleFor(x => x.CoverImageUrl!)
                .Must(BeValidImageUrl).WithMessage("CoverImageUrl must be a valid HTTP/HTTPS image URL (.jpg, .jpeg, .png, .gif, .webp).");
        });

        RuleFor(x => x)
            .MustAsync(PassBusinessRulesAsync)
            .WithMessage("Business rules failed for this order.");

        When(x => x.Category == OrderCategory.Technical, () =>
        {
            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(20m)
                .WithMessage("Technical orders must have a minimum price of $20.00.");

            RuleFor(x => x.Title)
                .Must(ContainTechnicalKeywords)
                .WithMessage("Technical orders must contain technical keywords in the Title.");

            RuleFor(x => x.PublishedDate)
                .Must(d => d >= DateTime.UtcNow.AddYears(-5))
                .WithMessage("Technical orders must be published within the last 5 years.");
        });

        When(x => x.Category == OrderCategory.Children, () =>
        {
            RuleFor(x => x.Price)
                .LessThanOrEqualTo(50m)
                .WithMessage("Children's orders must have a maximum price of $50.00.");

            RuleFor(x => x.Title)
                .Must(BeAppropriateForChildren)
                .WithMessage("Children's order titles must be appropriate for children.");
        });

        When(x => x.Category == OrderCategory.Fiction, () =>
        {
            RuleFor(x => x.Author)
                .MinimumLength(5)
                .WithMessage("Fiction authors must use their full name (min 5 characters).");
        });

        RuleFor(x => x)
            .Must(x => x.Price <= 100m || x.StockQuantity <= 20)
            .WithMessage("Expensive orders (price > $100) must have limited stock (≤ 20 units).");
    }

    private bool BeValidTitle(string title) =>
        !InappropriateWords.Any(w => title.Contains(w, StringComparison.OrdinalIgnoreCase));

    private async Task<bool> BeUniqueTitleAsync(CreateOrderProfileRequest req, string _, CancellationToken ct)
    {
        var ok = await _repo.IsTitleUniqueForAuthorAsync(req.Title, req.Author, ct);
        if (!ok)
        {
            _logger.LogWarning("Title+Author uniqueness failed | Title={Title} Author={Author}", req.Title, req.Author);
        }
        return ok;
    }

    private bool BeValidAuthorName(string author) =>
        Regex.IsMatch(author, @"^[A-Za-zÀ-ÖØ-öø-ÿ '\-\.]+$");

    private bool BeValidISBN(string isbn)
    {
        var s = NormalizeIsbn(isbn);
        return s.All(char.IsDigit) && (s.Length == 10 || s.Length == 13);
    }

    private async Task<bool> BeUniqueISBNAsync(string isbn, CancellationToken ct)
    {
        var normalized = NormalizeIsbn(isbn);
        var ok = await _repo.IsIsbnUniqueAsync(normalized, ct);
        if (!ok)
        {
            _logger.LogWarning("ISBN uniqueness failed | ISBN={ISBN}", isbn);
        }
        return ok;
    }

    private bool BeValidImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        if (u.Scheme is not ("http" or "https")) return false;

        var p = u.AbsolutePath.ToLowerInvariant();
        return p.EndsWith(".jpg") || p.EndsWith(".jpeg") ||
               p.EndsWith(".png") || p.EndsWith(".gif") ||
               p.EndsWith(".webp");
    }

    private async Task<bool> PassBusinessRulesAsync(CreateOrderProfileRequest req, CancellationToken ct)
    {
        var todayCount = await _repo.CountOrdersAddedOnDateAsync(DateTime.UtcNow, ct);
        if (todayCount >= 500)
        {
            _logger.LogWarning("Daily order limit reached: {Count}", todayCount);
            return false;
        }

        if (req.Category == OrderCategory.Technical && req.Price < 20m)
        {
            _logger.LogWarning("Technical minimum price rule failed | Price={Price}", req.Price);
            return false;
        }

        if (req.Category == OrderCategory.Children && !BeAppropriateForChildren(req.Title))
        {
            _logger.LogWarning("Children content restriction failed | Title={Title}", req.Title);
            return false;
        }

        if (req.Price > 500m && req.StockQuantity > 10)
        {
            _logger.LogWarning("High-value order stock limit failed | Price={Price} Stock={Stock}",
                req.Price, req.StockQuantity);
            return false;
        }

        return true;
    }

    private bool ContainTechnicalKeywords(string title) =>
        TechnicalKeywords.Any(k => title.Contains(k, StringComparison.OrdinalIgnoreCase));

    private bool BeAppropriateForChildren(string title) =>
        !InappropriateWords.Any(w => title.Contains(w, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeIsbn(string s) => s.Replace("-", "").Replace(" ", "");
}
