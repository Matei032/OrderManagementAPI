using Microsoft.Extensions.Logging;

namespace OrderManagementAPI.Common.Logging;

public static class LoggingExtensions
{
    public static void LogOrderCreationMetrics(
        this ILogger logger,
        OrderCreationMetrics metrics)
    {
        logger.LogInformation(
            new EventId(LogEvents.OrderCreationCompleted, nameof(LogEvents.OrderCreationCompleted)),
            "Order Metrics | OpId={OpId} | Title={Title} | ISBN={ISBN} | Category={Category} | " +
            "ValidationMs={ValidationMs} | DbMs={DbMs} | TotalMs={TotalMs} | Success={Success} | Error={Error}",
            metrics.OperationId,
            metrics.OrderTitle,
            metrics.ISBN,
            metrics.Category,
            metrics.ValidationDuration.TotalMilliseconds,
            metrics.DatabaseSaveDuration.TotalMilliseconds,
            metrics.TotalDuration.TotalMilliseconds,
            metrics.Success,
            metrics.ErrorReason ?? "None"
        );
    }
}
