namespace CSharpTemplate.Api.Endpoints;

public static class EndpointsContainer
{
    public static void RegisterEndpoints(this WebApplication app)
    {
        AuthEndpoints.Register(app);
    }
}
