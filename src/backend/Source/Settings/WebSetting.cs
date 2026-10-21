namespace Backend.Settings;

/// <summary>
/// Strongly-typed options bound from the <c>Web</c> configuration section
/// that describe the web-facing domains the application is allowed to serve.
/// </summary>
public class WebSetting
{
    /// <summary>
    /// The platform origin - the web app's own address when it acts in no tenant - always included in
    /// the allowed list. When <see cref="TenantOriginTemplate"/> is set, a request on this host is a
    /// platform-host request.
    /// </summary>
    public string DefaultDomain { get; set; } = null!;
    /// <summary>Additional domains allowed to access the API.</summary>
    public string[] Domains { get; set; } = [];

    /// <summary>
    /// The origin every tenant is served on, with the tenant's identifier standing in for
    /// <c>{tenant}</c> as the entire first host label (<c>https://{tenant}.example.com</c>). Empty turns
    /// host resolution off: every host then resolves to no tenant and sign-in, switching and sessions
    /// behave as though hosts named nothing.
    /// </summary>
    public string TenantOriginTemplate { get; set; } = string.Empty;

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
