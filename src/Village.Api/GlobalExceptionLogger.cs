using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace Village.Api;

/// <summary>
/// Wraps the entire pipeline to catch and log unhandled exceptions
/// before the framework's exception handler runs.
/// </summary>
public class ExceptionLoggingStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            builder.Use(async (context, nextMiddleware) =>
            {
                try
                {
                    await nextMiddleware();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"OUTER EXCEPTION: {ex.GetType().FullName}: {ex.Message}");
                    Console.Error.WriteLine(ex.StackTrace);
                    if (ex.InnerException != null)
                        Console.Error.WriteLine($"INNER: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                    throw;
                }
            });

            next(builder);
        };
    }
}

public class GlobalExceptionLogger : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionLogger> _logger;
    public GlobalExceptionLogger(ILogger<GlobalExceptionLogger> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception occurred");
        return false; // let the pipeline produce the standard error response
    }
}
