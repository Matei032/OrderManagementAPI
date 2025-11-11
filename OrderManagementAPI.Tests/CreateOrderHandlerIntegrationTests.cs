using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderManagementAPI.Common.Data;
using OrderManagementAPI.Common.Mapping;
using OrderManagementAPI.Features.Orders;
using OrderManagementAPI.Validators;
using Xunit;

namespace OrderManagementAPI.Tests;

public class CreateOrderHandlerIntegrationTests
{
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;
    private readonly OrderStore _store;
    private readonly InMemoryOrderReadRepository _repo;
    private readonly CreateOrderProfileValidator _validator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreateOrderHandlerIntegrationTests()
    {
        var expr = new MapperConfigurationExpression();
        expr.AddProfile<AdvancedOrderMappingProfile>();

        var config = new MapperConfiguration(expr, NullLoggerFactory.Instance);
        _mapper = config.CreateMapper();

        _cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));

        _store = new OrderStore();
        _repo = new InMemoryOrderReadRepository(_store);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "test-corr-id";
        _httpContextAccessor = new HttpContextAccessor { HttpContext = context };

        _validator = new CreateOrderProfileValidator(
            _repo,
            new NullLogger<CreateOrderProfileValidator>());
    }

    private CreateOrderHandler CreateHandler()
    {
        return new CreateOrderHandler(
            _mapper,
            _cache,
            new NullLogger<CreateOrderHandler>(),
            _httpContextAccessor,
            _store);
    }

    [Fact]
    public async Task CreateOrder_Succeeds_ForValidTechnicalOrder()
    {
        var handler = CreateHandler();
        var req = new CreateOrderProfileRequest
        {
            Title = "Clean Architecture patterns for microservices",
            Author = "John Doe",
            ISBN = "978-1234567890",
            Category = OrderCategory.Technical,
            Price = 49.99m,
            PublishedDate = DateTime.UtcNow.AddYears(-1),
            CoverImageUrl = "https://example.com/cover.png",
            StockQuantity = 5
        };

        var validationResult = await _validator.ValidateAsync(req, CancellationToken.None);
        Assert.True(validationResult.IsValid);

        var dto = await handler.HandleAsync(req, CancellationToken.None);

        Assert.Equal(req.Title, dto.Title);
        Assert.Equal(req.Author, dto.Author);
        Assert.Equal("Technical & Professional", dto.CategoryDisplayName);
        Assert.True(dto.IsAvailable);
        Assert.Equal(5, dto.StockQuantity);
    }

    [Fact]
    public async Task CreateOrder_Fails_ForDuplicateISBN()
    {
        var handler = CreateHandler();

        var req1 = new CreateOrderProfileRequest
        {
            Title = "Book 1",
            Author = "Someone",
            ISBN = "111-222-333-4",
            Category = OrderCategory.Fiction,
            Price = 25m,
            PublishedDate = DateTime.UtcNow.AddYears(-2),
            CoverImageUrl = "https://example.com/cover1.png",
            StockQuantity = 3
        };

        var req2 = new CreateOrderProfileRequest
        {
            Title = "Book 2",
            Author = "Other",
            ISBN = "111-222-333-4",
            Category = OrderCategory.Fiction,
            Price = 30m,
            PublishedDate = DateTime.UtcNow.AddYears(-1),
            CoverImageUrl = "https://example.com/cover2.png",
            StockQuantity = 2
        };

        var v1 = await _validator.ValidateAsync(req1);
        Assert.True(v1.IsValid);
        _ = await handler.HandleAsync(req1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(req2));
    }

    [Fact]
    public async Task CreateOrder_ChildrenCategory_AppliesDiscount_AndNullCover()
    {
        var handler = CreateHandler();
        var req = new CreateOrderProfileRequest
        {
            Title = "Cute kids stories",
            Author = "Happy Author",
            ISBN = "999-888-777-6",
            Category = OrderCategory.Children,
            Price = 40m,
            PublishedDate = DateTime.UtcNow.AddMonths(-2),
            CoverImageUrl = "https://example.com/kids.png",
            StockQuantity = 2
        };

        var validationResult = await _validator.ValidateAsync(req);
        Assert.True(validationResult.IsValid);

        var dto = await handler.HandleAsync(req);

        Assert.Equal(36m, dto.Price);
        Assert.Null(dto.CoverImageUrl);
        Assert.Equal("Children's Orders", dto.CategoryDisplayName);
        Assert.Equal("Limited Stock", dto.AvailabilityStatus); 
    }
}
