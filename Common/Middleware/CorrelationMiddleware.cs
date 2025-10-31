using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace OrderManagementAPI.Common.Middleware;

public class CorrelationMiddleware
{
    private const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationMiddleware> _logger;

    public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;

        if (context.Request.Headers.TryGetValue(HeaderName, out var existing) &&
            !string.IsNullOrWhiteSpace(existing.ToString()))
        {
            correlationId = existing.ToString();
        }
        else
        {
            correlationId = Guid.NewGuid().ToString("N")[..12]; 
            context.Request.Headers[HeaderName] = correlationId;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            var sw = Stopwatch.StartNew();

            _logger.LogInformation(
                "HTTP {Method} {Path} START CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                correlationId
            );

            await _next(context);

            sw.Stop();

            _logger.LogInformation(
                "HTTP {Method} {Path} END {StatusCode} CorrelationId={CorrelationId} DurationMs={Duration}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                correlationId,
                sw.ElapsedMilliseconds
            );
        }
    }
}

public static class CorrelationMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationMiddleware>();
    }
}
