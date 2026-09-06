namespace Backend.Features.Identity;

using Backend.Attributes;
using Backend.Features.Identity.Core;

/// <summary>
/// Feature module that registers the identity services with the DI container.
/// </summary>
[BypassNoDirectUse]
public class IdentityFeature : IFeature
{
    public static void AddServices(IServiceCollection services, ConfigurationManager configuration)
    {
        // configure settings
        services.AddOptions<AuthSetting>()
            .Bind(configuration.GetRequiredSection("Auth"))
            .Validate(setting => setting.Jwt is not null && setting.Jwt.Key?.Length >= 32,
                "The JWT signing key must contain at least 32 characters.")
            .Validate(setting => setting.Jwt is not null && !string.IsNullOrWhiteSpace(setting.Jwt.Issuer) && !string.IsNullOrWhiteSpace(setting.Jwt.Audience),
                "JWT issuer and audience are required.")
            .Validate(setting => setting.AccessTokenValidity > 0 && setting.RefreshTokenValidity > 0,
                "Authentication token lifetimes must be positive.")
            .ValidateOnStart();
        services.AddOptions<SigninSetting>()
            .Bind(configuration.GetRequiredSection("Signin"))
            .ValidateOnStart();
        
        // configure services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuthTokenService, AuthTokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthTokenCleanService, AuthTokenCleanService>();
        services.AddScoped<ITokenCleanService, TokenCleanService>();
    }
}
