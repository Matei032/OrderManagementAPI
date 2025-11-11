using System;
using System.Collections.Generic;

namespace OrderManagementAPI.Common.Errors;

public class ErrorResponse
{
    public string ErrorCode { get; set; } = "UNEXPECTED_ERROR";
    public string Message { get; set; } = "An unexpected error occurred.";
    public string? CorrelationId { get; set; }

    public Dictionary<string, string[]>? Errors { get; set; }
}
