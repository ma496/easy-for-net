using System.Net;
using EasyForNet.Application.Dependencies;
using EasyForNet.Exceptions.UserFriendly;
using EasyForNet.Helpers;

namespace CSharpTemplate.Api.ExceptionHandling;

public class ExceptionMiddleware : IMiddleware, ITransientDependency
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var errorId = Guid.NewGuid().ToString();
            var errorResult = new ErrorResult
            {
                Detail = env.IsDevelopment() ? exception.StackTrace : null,
                ErrorId = errorId,
                SupportMessage = $"Provide the Error Id: {errorId} to the support team for further analysis."
            };

            if (exception is not UserFriendlyException && exception.InnerException != null)
            {
                while (exception.InnerException != null)
                {
                    exception = exception.InnerException;
                }
            }

            switch (exception)
            {
                case UserFriendlyException:
                    errorResult.Status = 400;
                    errorResult.Message = exception.Message.Trim();
                    break;

                default:
                    errorResult.Status = (int)HttpStatusCode.InternalServerError;
                    errorResult.Message = env.IsDevelopment() ? exception.Message.Trim() : "Internal service error";
                    break;
            }

            var response = context.Response;
            if (!response.HasStarted)
            {
                response.ContentType = "application/json";
                response.StatusCode = errorResult.Status;
                await response.WriteAsync(JsonHelper.ToJson(errorResult));
            }
            else
            {
                Console.Write("Can't write error response. Response has already started.");
            }
        }
    }
}