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
/// <see cref="GateHeader"/>, and hands every other request to the host's own resolver. Nothing outside
/// the <c>TenantHosting</c> tests sends the header.
/// </remarks>
/// <param name="inner">The resolver the host registered.</param>
public sealed class TenantHostingOverride(ITenantHostResolver inner) : ITenantHostResolver
{
    /// <summary>The header that turns resolution on for one request; its value must be <c>on</c>.</summary>
    public const string GateHeader = "X-Test-Tenant-Hosting";

    /// <summary>The domain every test tenant host sits under.</summary>
    public const string Suffix = "tenant-hosting.test";

    /// <summary>The test platform host.</summary>
    public const string PlatformHost = "app." + Suffix;

    private static readonly TenantHostMap _map = new($"http://{PlatformHost}", $"http://{{tenant}}.{Suffix}");

    /// <summary>The host a tenant is served on under the test configuration.</summary>
    /// <param name="identifier">The tenant's identifier.</param>
    /// <returns>The host name.</returns>
    public static string TenantHost(string identifier) => $"{identifier}.{Suffix}";

    /// <inheritdoc />
    public TenantHost Resolve(HttpContext context)
        => string.Equals(context.Request.Headers[GateHeader], "on", StringComparison.Ordinal)
            ? _map.Resolve(context.Request.Host)
            : inner.Resolve(context);

    /// <inheritdoc />
    public string? TenantUrl(string identifier) => inner.TenantUrl(identifier);

    /// <inheritdoc />
    public string PlatformUrl() => inner.PlatformUrl();
}
