using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OrderManagementAPI.Common.Data;
using OrderManagementAPI.Common.Logging;
using OrderManagementAPI.Features.Orders.DTOs;
using System.Diagnostics;

namespace OrderManagementAPI.Features.Orders;

public class CreateOrderHandler
{
    private readonly IMapper _mapper;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CreateOrderHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly OrderStore _store; // <- DB in-memory

    public CreateOrderHandler(
        IMapper mapper,
        IMemoryCache cache,
        ILogger<CreateOrderHandler> logger,
        IHttpContextAccessor httpContextAccessor,
        OrderStore store)
    {
        _mapper = mapper;
        _cache = cache;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _store = store;
    }

    public async Task<OrderProfileDto> HandleAsync(CreateOrderProfileRequest request, CancellationToken ct = default)
    {
        // correlation id din middleware
        var correlationId =
            _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].ToString()
            ?? "n/a";

        var operationStart = Stopwatch.GetTimestamp();
        var operationId = Guid.NewGuid().ToString("N")[..8];

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["OpId"] = operationId,
            ["ISBN"] = request.ISBN,
            ["Category"] = request.Category.ToString(),
            ["CorrelationId"] = correlationId
        });

        _logger.LogInformation(
            new EventId(LogEvents.OrderCreationStarted, nameof(LogEvents.OrderCreationStarted)),
            "CreateOrder START | Title={Title} Author={Author} Category={Category} ISBN={ISBN}",
            request.Title, request.Author, request.Category, request.ISBN
        );

        // 1) Validation phase (ISBN unicitate)
        var validationStart = Stopwatch.GetTimestamp();

        var normalizedIsbn = NormalizeIsbn(request.ISBN);
        _logger.LogDebug(
            new EventId(LogEvents.ISBNValidationPerformed, nameof(LogEvents.ISBNValidationPerformed)),
            "Validating ISBN uniqueness: {ISBN}",
            request.ISBN
        );

        if (_store.Orders.Any(o => NormalizeIsbn(o.ISBN) == normalizedIsbn))
        {
            _logger.LogWarning(
                new EventId(LogEvents.OrderValidationFailed, nameof(LogEvents.OrderValidationFailed)),
                "CreateOrder FAILED | Duplicate ISBN={ISBN}",
                request.ISBN
            );

            var failedMetrics = BuildMetrics(
                request,
                operationId,
                validationStartTicks: validationStart,
                dbStartTicks: null,
                operationStartTicks: operationStart,
                success: false,
                error: "Duplicate ISBN"
            );

            _logger.LogOrderCreationMetrics(failedMetrics);
            throw new InvalidOperationException("An order with this ISBN already exists.");
        }

        var validationEnd = Stopwatch.GetTimestamp();

        // 2) "DB save" (în store-ul in-memory)
        var dbStart = Stopwatch.GetTimestamp();

        _logger.LogDebug(
            new EventId(LogEvents.DatabaseOperationStarted, nameof(LogEvents.DatabaseOperationStarted)),
            "Persisting new order to data store"
        );

        var entity = _mapper.Map<Order>(request);
        _store.Orders.Add(entity);

        _logger.LogDebug(
            new EventId(LogEvents.DatabaseOperationCompleted, nameof(LogEvents.DatabaseOperationCompleted)),
            "Persist complete | OrderId={OrderId}",
            entity.Id
        );

        var dbEnd = Stopwatch.GetTimestamp();

        // 3) Cache invalidation
        _cache.Remove("all_orders");
        _logger.LogInformation(
            new EventId(LogEvents.CacheOperationPerformed, nameof(LogEvents.CacheOperationPerformed)),
            "Cache invalidated for key 'all_orders'"
        );

        // 4) Map -> DTO
        var dto = _mapper.Map<OrderProfileDto>(entity);

        // 5) Metrics + telemetry
        var successMetrics = BuildMetrics(
            request,
            operationId,
            validationStartTicks: validationStart,
            dbStartTicks: dbStart,
            operationStartTicks: operationStart,
            success: true,
            error: null,
            validationEndTicks: validationEnd,
            dbEndTicks: dbEnd
        );

        _logger.LogOrderCreationMetrics(successMetrics);
        _logger.LogInformation(
            new EventId(LogEvents.OrderCreationCompleted, nameof(LogEvents.OrderCreationCompleted)),
            "CreateOrder OK | OrderId={OrderId} Title={Title}",
            dto.Id, dto.Title
        );

        return await Task.FromResult(dto);
    }

    public IEnumerable<OrderProfileDto> GetAll()
    {
        return _cache.GetOrCreate("all_orders", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return _store.Orders.Select(_mapper.Map<OrderProfileDto>).ToList();
        })!;
    }

    private static string NormalizeIsbn(string s) => s.Replace("-", "").Replace(" ", "");

    private static TimeSpan Elapsed(long startTicks, long endTicks) =>
        TimeSpan.FromSeconds((endTicks - startTicks) / (double)Stopwatch.Frequency);

    private OrderCreationMetrics BuildMetrics(
        CreateOrderProfileRequest req,
        string operationId,
        long validationStartTicks,
        long? dbStartTicks,
        long operationStartTicks,
        bool success,
        string? error,
        long? validationEndTicks = null,
        long? dbEndTicks = null)
    {
        var opEndTicks = Stopwatch.GetTimestamp();
        var vEnd = validationEndTicks ?? opEndTicks;
        var dStart = dbStartTicks ?? opEndTicks;
        var dEnd = dbEndTicks ?? opEndTicks;

        return new OrderCreationMetrics(
            OperationId: operationId,
            OrderTitle: req.Title,
            ISBN: req.ISBN,
            Category: req.Category,
            ValidationDuration: Elapsed(validationStartTicks, vEnd),
            DatabaseSaveDuration: Elapsed(dStart, dEnd),
            TotalDuration: Elapsed(operationStartTicks, opEndTicks),
            Success: success,
            ErrorReason: error
        );
    }
}
