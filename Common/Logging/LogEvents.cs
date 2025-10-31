namespace OrderManagementAPI.Common.Logging;

public static class LogEvents
{
    public const int OrderCreationStarted = 2001;
    public const int OrderValidationFailed = 2002;
    public const int OrderCreationCompleted = 2003;
    public const int DatabaseOperationStarted = 2004;
    public const int DatabaseOperationCompleted = 2005;
    public const int CacheOperationPerformed = 2006;
    public const int ISBNValidationPerformed = 2007;
    public const int StockValidationPerformed = 2008;
}
