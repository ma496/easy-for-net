using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using EasyForNet.Domain.Exceptions;

namespace EasyForNet.Host.ExceptionHandlers;

public class AppExceptionHandler : IExceptionHandler
{
    private readonly ILogger<AppExceptionHandler> _logger;

    public AppExceptionHandler(ILogger<AppExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is AppException appException)
        {
            httpContext.Response.StatusCode = appException.GetHttpStatusCode();

            if (appException.GetHttpStatusCode() == 400 && appException.Errors?.Count > 0)
            {
                await httpContext.Response.WriteAsJsonAsync(new ValidationProblemDetails(appException.Errors)
                {
                    Status = appException.GetHttpStatusCode(),
                    Type = GetType(appException.GetHttpStatusCode()),
                    Title = GetTitle(appException),
                    Detail = appException.Message,
                    Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
                });
            }
            else
            {
                await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = appException.GetHttpStatusCode(),
                    Type = GetType(appException.GetHttpStatusCode()),
                    Title = GetTitle(appException),
                    Detail = appException.Message,
                    Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
                });
            }

            return true;
        }

        return false;
    }

    private static string GetType(int httpStatusCode)
    {
        switch (httpStatusCode)
        {
            case 400:
                return "https://tools.ietf.org/html/rfc7231#section-6.5.1";
            case 401:
                return "https://tools.ietf.org/html/rfc7235#section-3.1";
            case 403:
                return "https://tools.ietf.org/html/rfc7231#section-6.5.3";
            case 404:
                return "https://tools.ietf.org/html/rfc7231#section-6.5.4";
            default:
                return "";
        }
    }

    private static string GetTitle(AppException appException)
    {
        switch (appException.GetHttpStatusCode())
        {
            case int c when c == 400 && appException.Errors.Count > 0:
                return "Validation Errors";
            case 400:
                return "Validation Error";
            case 401:
                return "Unauthorized";
            case 403:
                return "Forbidden";
            case 404:
                return "Not Found";
            default:
                return "Error";
        }
    }
}
