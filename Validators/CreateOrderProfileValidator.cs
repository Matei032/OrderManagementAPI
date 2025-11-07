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

    private static readonly string[] InappropriateWords = { "violent", "nsfw", "adult", "gore" };

    public CreateOrderProfileValidator(IOrderReadRepository repo, ILogger<CreateOrderProfileValidator> logger)
    {
        _repo = repo;
        _logger = logger;

        // Title
        RuleFor(x => x.Title)
            .NotEmpty().MaximumLength(200)
            .Must(BeCleanTitle).WithMessage("Title contains inappropriate content.")
            .MustAsync(BeUniqueTitleForAuthorAsync).WithMessage("An order with the same Title for this Author already exists.");

        // Author
        RuleFor(x => x.Author)
            .NotEmpty().MaximumLength(100)
            .Must(BeValidAuthorName).WithMessage("Author contains invalid characters.");

        // ISBN
        RuleFor(x => x.ISBN)
            .NotEmpty()
            .Must(BeValidIsbn).WithMessage("ISBN must be 10 or 13 digits (hyphens allowed).")
            .MustAsync(BeUniqueIsbnAsync).WithMessage("An order with this ISBN already exists.");

        // Category
        RuleFor(x => x.Category).IsInEnum();

        // Price
        RuleFor(x => x.Price).GreaterThan(0m).LessThan(10_000m);

        // PublishedDate
        RuleFor(x => x.PublishedDate)
            .Must(d => d.Date <= DateTime.UtcNow.Date).WithMessage("PublishedDate cannot be in the future.")
            .Must(d => d.Year >= 1400).WithMessage("PublishedDate cannot be before year 1400.");

        // Stock
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100_000);

        // CoverImageUrl (dacă e prezent)
        When(x => !string.IsNullOrWhiteSpace(x.CoverImageUrl), () =>
        {
            RuleFor(x => x.CoverImageUrl!)
                .Must(BeValidImageUrl).WithMessage("CoverImageUrl must be a valid HTTP/HTTPS image URL (.jpg, .jpeg, .png, .gif, .webp).");
        });

        // Business rules (exemple cerute în checklist)
        RuleFor(x => x).MustAsync(PassBusinessRulesAsync).WithMessage("Business rules failed for this order.");
    }

    // ===== helpers =====
    private bool BeCleanTitle(string title) =>
        InappropriateWords.All(w => !title.Contains(w, StringComparison.OrdinalIgnoreCase));

    private async Task<bool> BeUniqueTitleForAuthorAsync(CreateOrderProfileRequest req, string _, CancellationToken ct)
    {
        var ok = await _repo.IsTitleUniqueForAuthorAsync(req.Title, req.Author, ct);
        if (!ok) _logger.LogWarning("Title+Author uniqueness failed | Title={Title} Author={Author}", req.Title, req.Author);
        return ok;
    }

    private bool BeValidAuthorName(string author) =>
        Regex.IsMatch(author, @"^[A-Za-zÀ-ÖØ-öø-ÿ '\-\.]+$");

    private bool BeValidIsbn(string isbn)
    {
        var s = Normalize(isbn);
        return s.All(char.IsDigit) && (s.Length == 10 || s.Length == 13);
    }

    private async Task<bool> BeUniqueIsbnAsync(string isbn, CancellationToken ct)
    {
        var ok = await _repo.IsIsbnUniqueAsync(Normalize(isbn), ct);
        if (!ok) _logger.LogWarning("ISBN uniqueness failed | ISBN={ISBN}", isbn);
        return ok;
    }

    private bool BeValidImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        if (u.Scheme is not ("http" or "https")) return false;
        var p = u.AbsolutePath.ToLowerInvariant();
        return p.EndsWith(".jpg") || p.EndsWith(".jpeg") || p.EndsWith(".png") || p.EndsWith(".gif") || p.EndsWith(".webp");
    }

    private async Task<bool> PassBusinessRulesAsync(CreateOrderProfileRequest req, CancellationToken ct)
    {
        // exemplu: max 500 orders / zi
        var todayCount = await _repo.CountOrdersAddedOnDateAsync(DateTime.UtcNow, ct);
        if (todayCount >= 500) return false;

        // exemplu: Category=Technical => min price 20
        if (req.Category == OrderCategory.Technical && req.Price < 20m) return false;

        // exemplu: Children => titlu „curat”
        if (req.Category == OrderCategory.Children && !BeCleanTitle(req.Title)) return false;

        // exemplu: preț foarte mare => stoc rezonabil
        if (req.Price > 500m && req.StockQuantity > 10) return false;

        return true;
    }

    private static string Normalize(string s) => s.Replace("-", "").Replace(" ", "");
}
