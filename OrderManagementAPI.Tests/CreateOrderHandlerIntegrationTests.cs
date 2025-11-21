using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using OrderManagementAPI.Common.Data;
using OrderManagementAPI.Common.Mapping;
using OrderManagementAPI.Features.Orders;
using OrderManagementAPI.Features.Orders.DTOs;
using Xunit;

namespace OrderManagementAPI.Tests;

public class CreateOrderHandlerIntegrationTests : IDisposable
{
    private readonly OrderStore _store;
    private readonly IMemoryCache _cache;
    private readonly IMapper _mapper;
    private readonly Mock<ILogger<CreateOrderHandler>> _loggerMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly CreateOrderHandler _handler;

    public CreateOrderHandlerIntegrationTests()
    {
        _store = new OrderStore();
        _cache = new MemoryCache(new MemoryCacheOptions());

        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<AdvancedOrderMappingProfile>();
        });
        _mapper = config.CreateMapper();

        _loggerMock = new Mock<ILogger<CreateOrderHandler>>();

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Correlation-Id"] = "test-correlation-id";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        _handler = new CreateOrderHandler(
            _mapper,
            _cache,
            _loggerMock.Object,
            _httpContextAccessorMock.Object,
            _store
        );
    }

    [Fact]
    public async Task Handle_ValidTechnicalOrderRequest_CreatesOrderWithCorrectMappings()
    {
        var request = new CreateOrderProfileRequest
        {
            Title = "Clean Architecture Patterns",
            Author = "Robert Martin",
            ISBN = "978-0134494166",
            Category = OrderCategory.Technical,
            Price = 45.99m,
            PublishedDate = DateTime.UtcNow.AddMonths(-6),
            CoverImageUrl = "https://example.com/cover.jpg",
            StockQuantity = 15
        };

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Clean Architecture Patterns", result.Title);
        Assert.Equal("Robert Martin", result.Author);
        Assert.Equal("978-0134494166", result.ISBN);
        Assert.Equal("Technical & Professional", result.CategoryDisplayName);
        Assert.Equal("RM", result.AuthorInitials);
        Assert.Contains("months old", result.PublishedAge);
        Assert.Matches(@"^[\$£€¥]", result.FormattedPrice);
        Assert.Equal("In Stock", result.AvailabilityStatus);
        Assert.True(result.IsAvailable);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.Is<EventId>(e => e.Id == 2001),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateISBN_ThrowsInvalidOperationExceptionWithLogging()
    {
        var existingOrder = new Order
        {
            Id = Guid.NewGuid(),
            Title = "Existing Book",
            Author = "John Doe",
            ISBN = "978-1234567890",
            Category = OrderCategory.Fiction,
            Price = 25.00m,
            PublishedDate = DateTime.UtcNow.AddYears(-1),
            StockQuantity = 5,
            IsAvailable = true,
            CreatedAt = DateTime.UtcNow
        };
        _store.Orders.Add(existingOrder);

        var request = new CreateOrderProfileRequest
        {
            Title = "New Book",
            Author = "Jane Doe",
            ISBN = "978-1234567890",
            Category = OrderCategory.Fiction,
            Price = 30.00m,
            PublishedDate = DateTime.UtcNow.AddMonths(-3),
            StockQuantity = 10
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.HandleAsync(request, CancellationToken.None)
        );

        Assert.Contains("already exists", exception.Message);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.Is<EventId>(e => e.Id == 2002),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ChildrensOrderRequest_AppliesDiscountAndConditionalMapping()
    {
        var originalPrice = 40.00m;
        var request = new CreateOrderProfileRequest
        {
            Title = "The Magic Adventure",
            Author = "Mary Smith",
            ISBN = "978-9876543210",
            Category = OrderCategory.Children,
            Price = originalPrice,
            PublishedDate = DateTime.UtcNow.AddMonths(-2),
            CoverImageUrl = "https://example.com/kids-book.jpg",
            StockQuantity = 25
        };

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Children's Orders", result.CategoryDisplayName);

        var expectedPrice = Math.Round(originalPrice * 0.9m, 2);
        Assert.Equal(expectedPrice, result.Price);

        Assert.Null(result.CoverImageUrl);
        Assert.True(result.IsAvailable);
        Assert.Equal("In Stock", result.AvailabilityStatus);
    }

    public void Dispose()
    {
        _cache?.Dispose();
        _store.Orders.Clear();
    }
}