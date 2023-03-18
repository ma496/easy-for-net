using System.Reflection;
using EasyForNet.Extensions;

namespace CSharpTemplate.Api.Endpoints.Automation;

public static class EndpointsContainer
{
    public static void RegisterEndpoints(this RouteGroupBuilder root)
    {
        // AuthEndpoints.Register(root);
        // UserEndpoints.Register(root);
        
        AutoRegister(root);
    }

    private static void AutoRegister(RouteGroupBuilder root)
    {
        var types = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsStaticClass())
            .ToList();
        types.ForEach(t =>
        {
            var methods = t.GetMethods()
                .Where(m => m.GetCustomAttribute<MinimalApiAttribute>() != null)
                .ToList();
            methods.ForEach(m =>
            {
                m.Invoke(null, new object?[]{root});
            });
        });
    }
}
