using AutoMapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OrderManagementAPI.Features.Orders.DTOs;

namespace OrderManagementAPI.Features.Orders;

public class CreateOrderHandler
{
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CreateOrderHandler> _logger;

    private static readonly List<Order> _orders = new();

    public CreateOrderHandler(IMapper mapper, IMemoryCache cache, ILogger<CreateOrderHandler> logger)
    {
        _mapper = mapper;
        _cache = cache;
        _logger = logger;
    }

    public Task<OrderProfileDto> HandleAsync(CreateOrderProfileRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("CreateOrder START | Title={Title} Author={Author} Category={Category} ISBN={ISBN}",
            request.Title, request.Author, request.Category, request.ISBN);

        var isbn = NormalizeIsbn(request.ISBN);
        if (_orders.Any(o => NormalizeIsbn(o.ISBN) == isbn))
        {
            _logger.LogWarning("CreateOrder FAIL | Duplicate ISBN={ISBN}", request.ISBN);
            throw new InvalidOperationException("An order with this ISBN already exists.");
        }

        var entity = _mapper.Map<Order>(request);
        _orders.Add(entity);

        _cache.Remove("all_orders");

        var dto = _mapper.Map<OrderProfileDto>(entity);

        _logger.LogInformation("CreateOrder OK | OrderId={OrderId} Title={Title}", dto.Id, dto.Title);
        return Task.FromResult(dto);
    }

    public IEnumerable<OrderProfileDto> GetAll()
        => _cache.GetOrCreate("all_orders", e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return _orders.Select(_mapper.Map<OrderProfileDto>).ToList();
        })!;

    private static string NormalizeIsbn(string s) => s.Replace("-", "").Replace(" ", "");
}
