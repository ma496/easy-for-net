using Backend.Features.Tenancy.Core.Hosting;
using Microsoft.AspNetCore.Http;

namespace Backend.Tests.Fakes;

/// <summary>
/// Turns tenant host resolution on for the requests that ask for it, and only those.
/// </summary>
/// <remarks>
/// The shared host runs with <c>Web:TenantOriginTemplate</c> empty, so every other test class sees hosts
/// name nothing. A second host with resolution on would seed and drop the same database concurrently, so
/// instead this decorator resolves against a fixed test configuration - the platform on
/// <see cref="PlatformHost"/>, tenants on <c>{identifier}.</c><see cref="Suffix"/> - for a request carrying
/// <see cref="GateHeader"/>, and hands every other request to the host's own resolver. The members that take
/// no request (<see cref="ResolveHost"/>, <see cref="TenantUrl"/>, <see cref="TenantUrlTemplate"/>,
/// <see cref="PlatformUrl"/>) read the
/// header off the request being served, so links an endpoint builds follow the test configuration too.
/// Nothing outside the <c>TenantHosting</c> tests sends the header.
/// </remarks>
/// <param name="inner">The resolver the host registered.</param>
/// <param name="httpContextAccessor">The request being served, for the members that take none.</param>
public sealed class TenantHostingOverride(ITenantHostResolver inner, IHttpContextAccessor httpContextAccessor) : ITenantHostResolver
{
    /// <summary>The header that turns resolution on for one request; its value must be <c>on</c>.</summary>
    public const string GateHeader = "X-Test-Tenant-Hosting";

    /// <summary>The domain every test tenant host sits under.</summary>
    public const string Suffix = "tenant-hosting.test";

    /// <summary>The test platform host.</summary>
    public const string PlatformHost = "app." + Suffix;

    /// <summary>The test platform origin.</summary>
    public const string PlatformOrigin = "http://" + PlatformHost;

    private static readonly TenantHostMap _map = new(PlatformOrigin, $"http://{{tenant}}.{Suffix}");

    /// <summary>The host a tenant is served on under the test configuration.</summary>
    /// <param name="identifier">The tenant's identifier.</param>
    /// <returns>The host name.</returns>
    public static string TenantHost(string identifier) => $"{identifier}.{Suffix}";

    /// <summary>The origin a tenant is served on under the test configuration.</summary>
    /// <param name="identifier">The tenant's identifier.</param>
    /// <returns>The origin.</returns>
    public static string TenantOrigin(string identifier) => $"http://{TenantHost(identifier.ToLowerInvariant())}";

    /// <inheritdoc />
    public TenantHost Resolve(HttpContext context)
        => IsGated(context) ? _map.Resolve(context.Request.Host) : inner.Resolve(context);

    /// <inheritdoc />
    public TenantHost ResolveHost(string? host)
        => IsGated(httpContextAccessor.HttpContext) ? _map.ResolveHost(host) : inner.ResolveHost(host);

    /// <inheritdoc />
    public string? TenantUrl(string identifier)
        => IsGated(httpContextAccessor.HttpContext) ? _map.TenantUrl(identifier) : inner.TenantUrl(identifier);

    /// <inheritdoc />
    public string PlatformUrl()
        => IsGated(httpContextAccessor.HttpContext) ? _map.PlatformUrl() : inner.PlatformUrl();

    /// <inheritdoc />
    public string? TenantUrlTemplate()
        => IsGated(httpContextAccessor.HttpContext) ? _map.TenantUrlTemplate() : inner.TenantUrlTemplate();

    /// <inheritdoc />
    /// <remarks>
    /// A gated request on a host the test configuration does not name falls back to the host's own
    /// <c>Web:DefaultDomain</c>, as an unknown host does in production.
    /// </remarks>
    public string RequestOrigin(HttpContext context)
    {
        if (!IsGated(context))
        {
            return inner.RequestOrigin(context);
        }

        var host = _map.Resolve(context.Request.Host);
        return host.IsResolved ? _map.Origin(host) : inner.PlatformUrl();
    }

    private static bool IsGated(HttpContext? context)
        => context is not null && string.Equals(context.Request.Headers[GateHeader], "on", StringComparison.Ordinal);
}
