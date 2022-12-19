using CSharpTemplate.Data.Context;
using CSharpTemplate.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CSharpTemplate.App.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddDbContextAndIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CSharpTemplateDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
        });

        services.AddIdentity<AppUser, AppRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequiredLength = 6;
        }).AddEntityFrameworkStores<CSharpTemplateDbContext>()
            .AddDefaultTokenProviders();
    }
}
