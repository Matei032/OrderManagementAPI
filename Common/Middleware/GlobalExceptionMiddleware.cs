using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrderManagementAPI.Common.Errors;

namespace OrderManagementAPI.Common.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private const string CorrelationHeader = "X-Correlation-Id";

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await HandleValidationExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            await HandleUnhandledExceptionAsync(context, ex);
        }
    }

    private async Task HandleValidationExceptionAsync(HttpContext context, ValidationException ex)
    {
        var correlationId = GetCorrelationId(context);

        _logger.LogWarning(ex,
            "Validation error | CorrelationId={CorrelationId} | Path={Path}",
            correlationId,
            context.Request.Path
        );

        var errorDict = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

        var response = new ErrorResponse
        {
            ErrorCode = "VALIDATION_ERROR",
            Message = "One or more validation errors occurred.",
            CorrelationId = correlationId,
            Errors = errorDict
        };

        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(json);
    }

    private async Task HandleUnhandledExceptionAsync(HttpContext context, Exception ex)
    {
        var correlationId = GetCorrelationId(context);

        _logger.LogError(ex,
            "Unhandled exception | CorrelationId={CorrelationId} | Path={Path}",
            correlationId,
            context.Request.Path
        );

        var response = new ErrorResponse
        {
            ErrorCode = "UNEXPECTED_ERROR",
            Message = "An unexpected error occurred while processing your request.",
            CorrelationId = correlationId
        };

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(json);
    }

    private static string? GetCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationHeader, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return value.ToString();
        }

        return null;
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}
