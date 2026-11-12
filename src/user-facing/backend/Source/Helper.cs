namespace Backend;

/// <summary>
/// Application-wide static helpers used during startup to wire up feature
/// modules.
/// </summary>
public static class Helper
{
    /// <summary>
    /// Discovers every type implementing <see cref="IFeature"/> in the current
    /// assembly and invokes its static <c>AddServices</c> method to register
    /// its services with the DI container.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">The application's configuration manager, forwarded to each feature.</param>
    public static void AddFeatures(IServiceCollection services, ConfigurationManager configuration)
    {
        var features = typeof(Helper).Assembly.GetTypes()
            .Where(p => typeof(IFeature).IsAssignableFrom(p) && !p.IsAbstract)
            .ToList();

        foreach (var feature in features)
        {
            feature.GetMethod("AddServices")?.Invoke(null, [services, configuration]);
        }
    }
}
