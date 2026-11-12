namespace Backend.Base;

/// <summary>
/// Contract implemented by every backend feature module. Each feature exposes
/// a static <c>AddServices</c> entry point so that the host can discover and
/// register its DI services at startup.
/// </summary>
public interface IFeature
{
    static abstract void AddServices(IServiceCollection services, ConfigurationManager configuration);
}