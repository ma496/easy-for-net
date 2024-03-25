using Newtonsoft.Json;
using System.Net;

namespace Efn.Middlewares;

public static class UnauthenticatedAndUnauthorizedResponse
{
    public static IApplicationBuilder UseAuthenticatedAndUnauthorizedResponse(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            await next();

            if (context.Response.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                context.Response.Headers.Append("access-control-allow-origin", "*");
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new CustomResponse("Unauthenticated request!")));
            }
            if (context.Response.StatusCode == (int)HttpStatusCode.Forbidden)
            {
                context.Response.Headers.Append("access-control-allow-origin", "*");
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new CustomResponse("Forbidden request!")));
            }
        });

        return app;
    }

    private record CustomResponse (string message);
}
