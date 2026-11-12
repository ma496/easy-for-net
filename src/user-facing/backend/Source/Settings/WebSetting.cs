namespace Backend.Settings;

/// <summary>
/// Strongly-typed options bound from the <c>Web</c> configuration section
/// that describe the web-facing domains the application is allowed to serve.
/// </summary>
public class WebSetting
{
    /// <summary>
    /// The site origin - the web app's own address, which every emailed link is built from - always
    /// included in the allowed list.
    /// </summary>
    public string DefaultDomain { get; set; } = null!;
    /// <summary>Additional domains allowed to access the API.</summary>
    public string[] Domains { get; set; } = [];

    /// <summary>
    /// The site origin with no trailing slash, ready to have a path appended.
    /// </summary>
    /// <returns><see cref="DefaultDomain"/> without its trailing slash.</returns>
    public string SiteOrigin() => DefaultDomain.TrimEnd('/');

    /// <summary>
    /// Combines <see cref="DefaultDomain"/> and <see cref="Domains"/> into a
    /// single distinct list of allowed domains, typically used to configure
    /// the CORS policy.
    /// </summary>
    /// <returns>The de-duplicated list of allowed domain strings.</returns>
    public string[] AllowedDomains()
    {
        var returnDomains = new List<string> { DefaultDomain };
        returnDomains.AddRange(Domains);
        return returnDomains.Distinct().ToArray();
    }
}
