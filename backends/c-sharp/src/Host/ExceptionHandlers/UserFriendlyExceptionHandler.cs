using System.Net;
using EasyForNet.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EasyForNet.Host;

public class UserFriendlyExceptionHandler : IExceptionHandler
{
    private readonly ILogger<UserFriendlyExceptionHandler> _logger;

    public UserFriendlyExceptionHandler(ILogger<UserFriendlyExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is UserFriendlyException userFriendlyException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;

            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = (int)HttpStatusCode.UnprocessableEntity,
                Type = "https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/422",
                Title = "Error",
                Detail = userFriendlyException.Message,
                Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
            });

            return true;
        }

        return false;
    }
}
