namespace Backend.Tests.Fakes;

using Backend.External.Email;
using Backend.Features.Identity.Core;

/// <summary>
/// Registers the implementations the test host substitutes for production ones.
/// </summary>
/// <remarks>
/// These run through <c>ConfigureTestServices</c>, which is applied after the application's own
/// registrations, so each one replaces what the feature registered rather than competing with it.
/// </remarks>
public static class TestDoubles
{
    /// <summary>
    /// Substitutes the cheap password hasher and the mail service that sends nothing.
    /// </summary>
    public static IServiceCollection RegisterTestDoubles(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher, TestPasswordHasher>();
        services.AddScoped<IEmailService, NoOpEmailService>();
        return services;
    }
}
