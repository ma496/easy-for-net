using EasyForNet.Application.Common.Interfaces;
using EasyForNet.Host.Services;
using EasyForNet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace EasyForNet.Host;

public static class ConfigureServices
{
    public static IServiceCollection AddHostServices(this IServiceCollection services)
    {
        services.AddControllers();
        
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        
        services.AddHttpContextAccessor();

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>();

        // Customise default API behaviour
        services.Configure<ApiBehaviorOptions>(options =>
            options.SuppressModelStateInvalidFilter = true);
        
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}
