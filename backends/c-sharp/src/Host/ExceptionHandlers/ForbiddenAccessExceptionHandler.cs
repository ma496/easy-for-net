﻿using System.Net;
using EasyForNet.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EasyForNet.Host;

public class ForbiddenAccessExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ForbiddenAccessExceptionHandler> _logger;

    public ForbiddenAccessExceptionHandler(ILogger<ForbiddenAccessExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ForbiddenAccessException forbiddenAccessException)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;

            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = (int)HttpStatusCode.Forbidden,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                Title = "Forbidden",
                Detail = forbiddenAccessException.Message,
                Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
            });

            return true;
        }

        return false;
    }
}