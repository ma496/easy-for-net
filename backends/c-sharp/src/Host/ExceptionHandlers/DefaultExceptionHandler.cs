using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EasyForNet.Host;

public class DefaultExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DefaultExceptionHandler> _logger;
    private readonly IWebHostEnvironment _environment;

    public DefaultExceptionHandler(ILogger<DefaultExceptionHandler> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unexpected error occurred");

        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = (int)HttpStatusCode.InternalServerError,
            Type = exception.GetType().Name,
            Title = "Error",
            Detail = GetExceptionDetail(exception),
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        });

        return true;
    }

    private string GetExceptionDetail(Exception exception)
    {
        if (exception.InnerException == null)
        {
            return _environment.IsDevelopment() ? GetExceptionMessage(exception.Message, exception.StackTrace) : "Internal server error.";
        }

        return GetExceptionDetail(exception);
    }

    private string GetExceptionMessage(string message, string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return message;

        return $"{message}{Environment.NewLine}...{stackTrace}";
    }
}
