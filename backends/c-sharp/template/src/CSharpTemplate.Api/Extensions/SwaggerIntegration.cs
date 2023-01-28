using Microsoft.OpenApi.Models;

namespace CSharpTemplate.Api.Extensions;

public static class SwaggerIntegration
{
    public static void AddSwaggerWithSecurity(this IServiceCollection services)
    {
        var securityScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JSON Web Token based security",
        };

        var securityReq = new OpenApiSecurityRequirement
        {
            { // key and value pair
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] {}
            }
        };

        var contact = new OpenApiContact
        {
            Name = "",
            Email = "",
            Url = null
        };

        var license = new OpenApiLicense
        {
            Name = "",
            Url = null
        };

        var info = new OpenApiInfo
        {
            Version = "v1",
            Title = "CSharpTemplate API",
            Description = "Documentation of endpoints",
            TermsOfService = null,
            Contact = contact,
            License = license
        };

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", info);
            o.AddSecurityDefinition("Bearer", securityScheme);
            o.AddSecurityRequirement(securityReq);
        });
    }
}
