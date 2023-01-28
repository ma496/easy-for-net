namespace CSharpTemplate.Api.Endpoints;

public static class EndpointsContainer
{
    public static void RegisterEndpoints(this RouteGroupBuilder root)
    {
        AuthEndpoints.Register(root);
        UserEndpoints.Register(root);
    }
}
