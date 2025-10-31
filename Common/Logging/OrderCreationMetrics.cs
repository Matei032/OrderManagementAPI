using System;
using OrderManagementAPI.Features.Orders;

namespace OrderManagementAPI.Common.Logging;

public record OrderCreationMetrics(
    string OperationId,
    string OrderTitle,
    string ISBN,
    OrderCategory Category,
    TimeSpan ValidationDuration,
    TimeSpan DatabaseSaveDuration,
    TimeSpan TotalDuration,
    bool Success,
    string? ErrorReason = null
);
