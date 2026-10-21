using Backend.Settings;

namespace Backend.Features.Tenancy.Core.Hosting;

/// <summary>
/// Reads which tenant - or the platform - a request's host names. Pure parsing of the host against
/// <c>Web:DefaultDomain</c> and <c>Web:TenantOriginTemplate</c>; it never reads the database, so it costs
/// an ordinary request nothing.
/// </summary>
/// <remarks>
/// The host chooses, it never grants. What it names is checked against the session
/// (<c>TenantHostSessionMiddleware</c>) and, at sign-in, against the account's membership; it is never
/// a reason to let anybody in.
/// </remarks>
[AllowOutside]
public interface ITenantHostResolver
{
    /// <summary>
    /// What the request's host names. <see cref="TenantHost.None"/> when resolution is off (an empty
    /// template) or the host is not one of ours.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The resolution.</returns>
    TenantHost Resolve(HttpContext context);

    /// <summary>The origin a tenant is served on, or <see langword="null"/> when resolution is off.</summary>
    /// <param name="identifier">The tenant's identifier.</param>
    /// <returns>The tenant's origin.</returns>
    string? TenantUrl(string identifier);

    /// <summary>
    /// The tenant origin template with its <c>{tenant}</c> placeholder left in place, for a caller that
    /// shows a tenant's origin before the tenant exists; <see langword="null"/> when resolution is off.
    /// </summary>
    /// <returns>The tenant origin template.</returns>
    string? TenantUrlTemplate();

    /// <summary>The platform origin.</summary>
    /// <returns>The platform origin.</returns>
    string PlatformUrl();

    /// <summary>
    /// What a host name names, by the same rules as <see cref="Resolve(HttpContext)"/> applied to a host
    /// handed in rather than the request's own - the question asked about a domain a certificate is
    /// requested for. <see cref="TenantHost.None"/> for an empty or malformed host and whenever resolution
    /// is off.
    /// </summary>
    /// <param name="host">A host name, with an optional port.</param>
    /// <returns>The resolution.</returns>
    TenantHost ResolveHost(string? host);

    /// <summary>
    /// The origin links built for this request point at: the tenant's origin on a tenant host, the platform
    /// origin on the platform host, and <c>Web:DefaultDomain</c> when the host names nothing. Read from the
    /// host alone - never from the caller's session - so an anonymous endpoint that mails a link sends the
    /// visitor back to the host the request came from, whichever tenant a signed-in caller acts in.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The origin.</returns>
    string RequestOrigin(HttpContext context);
}

/// <summary>
/// Default <see cref="ITenantHostResolver"/>, over a <see cref="TenantHostMap"/> built once from the
/// <c>Web</c> settings (bound and validated at startup).
/// </summary>
/// <param name="webSetting">The <c>Web</c> settings.</param>
[NoDirectUse]
public sealed class TenantHostResolver(IOptions<WebSetting> webSetting) : ITenantHostResolver
{
    private readonly TenantHostMap _map = new(webSetting.Value);

    /// <inheritdoc />
    public TenantHost Resolve(HttpContext context) => _map.Resolve(context.Request.Host);

    /// <inheritdoc />
    public string? TenantUrl(string identifier) => _map.TenantUrl(identifier);

    /// <inheritdoc />
    public string? TenantUrlTemplate() => _map.TenantUrlTemplate();

    /// <inheritdoc />
    public string PlatformUrl() => _map.PlatformUrl();

    /// <inheritdoc />
    public TenantHost ResolveHost(string? host) => _map.ResolveHost(host);

    /// <inheritdoc />
    public string RequestOrigin(HttpContext context) => _map.Origin(Resolve(context));
}
