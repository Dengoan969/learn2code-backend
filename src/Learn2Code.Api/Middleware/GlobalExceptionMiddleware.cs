using System.Net;
using System.Text.Json;

namespace Learn2Code.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly RequestDelegate _next;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex, _environment.IsDevelopment());
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, bool isDevelopment)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            error = "Internal Server Error",
            message = isDevelopment ? exception.Message : "An error occurred. Please try again later.",
            stackTrace = isDevelopment ? exception.StackTrace : null,
            innerException = isDevelopment ? GetInnerExceptionDetails(exception) : null
        };

        var json = JsonSerializer.Serialize(response, Learn2Code.Core.JsonOptions.Default);
        await context.Response.WriteAsync(json);
    }

    private static object? GetInnerExceptionDetails(Exception? exception)
    {
        if (exception == null)
            return null;

        var innerExceptions = new List<object>();
        var current = exception;
        
        while (current != null)
        {
            innerExceptions.Add(new
            {
                message = current.Message,
                type = current.GetType().FullName,
                stackTrace = current.StackTrace
            });
            current = current.InnerException;
        }

        return innerExceptions.Count > 1 ? innerExceptions.Skip(1) : null;
    }
}