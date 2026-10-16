using Backend.Settings;

namespace Backend.Features.Tenancy.Core.Hosting;

/// <summary>
/// The pure mapping between hosts and what they name, built from <c>Web:DefaultDomain</c> and
/// <c>Web:TenantOriginTemplate</c>. No request, no database: a host in, an answer out, so the parsing
/// rules are stated and tested in one place.
/// </summary>
/// <remarks>
/// <para>
/// A host matches an origin when its name equals the origin's host (compared case-insensitively) and
/// its port equals the origin's port. A request host with no port is taken to be on the origin's
/// default port, so <c>acme.example.com</c> matches <c>https://{tenant}.example.com</c> and
/// <c>acme.example.com:8443</c> does not. The scheme is deliberately not compared: behind a proxy that
/// terminates TLS the scheme the API sees depends on forwarded headers, and the authority alone is
/// what tells one host from another.
/// </para>
/// <para>
/// A tenant host is exactly one label in front of the template's suffix: <c>a.b.example.com</c>,
/// <c>acmeexample.com</c> and <c>acme.example.com.evil.test</c> name nothing. The platform host is
/// checked first, so its own first label (<c>app</c> in <c>app.example.com</c>) is never read as a tenant.
/// </para>
/// </remarks>
public sealed class TenantHostMap
{
    /// <summary>The placeholder the template carries where a tenant's identifier goes.</summary>
    public const string TenantPlaceholder = "{tenant}";

    /// <summary>The label substituted for the placeholder when the template is checked for shape.</summary>
    private const string SampleLabel = "tenant";

    /// <summary>The longest text, a port included, <see cref="ResolveHost"/> reads as a host.</summary>
    private const int MaxHostLength = 260;

    private readonly string _template;
    private readonly string _defaultDomain;
    private readonly Authority? _platform;
    private readonly Authority? _tenantSuffix;

    /// <summary>
    /// Builds the map from the web settings.
    /// </summary>
    /// <param name="setting">The bound <c>Web</c> section.</param>
    public TenantHostMap(WebSetting setting)
        : this(setting.DefaultDomain, setting.TenantOriginTemplate)
    {
    }

    /// <summary>
    /// Builds the map from the platform origin and the tenant origin template. An empty template turns
    /// resolution off; a template failing <see cref="IsValidTemplate"/> does too (startup validation
    /// refuses one before it gets here).
    /// </summary>
    /// <param name="defaultDomain">The platform origin.</param>
    /// <param name="tenantOriginTemplate">The tenant origin template, or empty to turn resolution off.</param>
    public TenantHostMap(string? defaultDomain, string? tenantOriginTemplate)
    {
        _defaultDomain = defaultDomain?.Trim() ?? string.Empty;
        _template = tenantOriginTemplate?.Trim() ?? string.Empty;

        if (_template.Length == 0 || !TryParseTemplate(_template, out var suffix))
        {
            return;
        }

        _tenantSuffix = suffix;
        if (Uri.TryCreate(_defaultDomain, UriKind.Absolute, out var platformUri))
        {
            _platform = new Authority(platformUri.Host.ToLowerInvariant(), platformUri.Port, platformUri.IsDefaultPort);
        }
    }

    /// <summary>Whether host resolution is on.</summary>
    public bool IsEnabled => _tenantSuffix is not null;

    /// <summary>
    /// Whether a configured template has the shape resolution needs: empty (resolution off), or exactly
    /// one <c>{tenant}</c> forming the entire first host label, with the value as a whole - the
    /// placeholder filled in - an absolute http(s) URI naming an origin and nothing more.
    /// </summary>
    /// <param name="template">The configured template.</param>
    /// <returns><see langword="true"/> when the template may be used.</returns>
    public static bool IsValidTemplate(string? template)
        => string.IsNullOrWhiteSpace(template) || TryParseTemplate(template.Trim(), out _);

    /// <summary>
    /// What a request host names.
    /// </summary>
    /// <param name="host">The request's host and optional port.</param>
    /// <returns>The resolution; <see cref="TenantHost.None"/> whenever resolution is off.</returns>
    public TenantHost Resolve(HostString host)
    {
        if (_tenantSuffix is not { } suffix || !host.HasValue)
        {
            return TenantHost.None;
        }

        var name = host.Host.ToLowerInvariant();
        var port = host.Port;

        if (_platform is { } platform && platform.Matches(name, port))
        {
            return TenantHost.Platform;
        }

        if (!suffix.MatchesPort(port) || !name.EndsWith("." + suffix.Host, StringComparison.Ordinal))
        {
            return TenantHost.None;
        }

        var label = name[..^(suffix.Host.Length + 1)];
        return IsTenantLabel(label) ? TenantHost.ForTenant(label) : TenantHost.None;
    }

    /// <summary>
    /// What a host handed in as text names, by the same rules as <see cref="Resolve(HostString)"/>. An
    /// empty value, one too long to be a host, or one carrying anything but a host name and an optional
    /// port (a scheme, a path, user information, white-space) names nothing.
    /// </summary>
    /// <param name="host">A host name, with an optional port.</param>
    /// <returns>The resolution; <see cref="TenantHost.None"/> whenever resolution is off.</returns>
    public TenantHost ResolveHost(string? host)
    {
        if (string.IsNullOrEmpty(host) || host.Length > MaxHostLength
            || host.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or ':')))
        {
            return TenantHost.None;
        }

        return Resolve(new HostString(host));
    }

    /// <summary>
    /// The origin links point at for a resolution: the tenant's origin for a tenant host, and the
    /// platform origin for the platform host and for a host that names nothing.
    /// </summary>
    /// <param name="host">What the host names.</param>
    /// <returns>The origin.</returns>
    public string Origin(TenantHost host)
        => host is { Kind: TenantHostKind.Tenant, Identifier: { } identifier } && TenantUrl(identifier) is { } tenantUrl
            ? tenantUrl
            : PlatformUrl();

    /// <summary>
    /// The origin a tenant is served on, or <see langword="null"/> when resolution is off.
    /// </summary>
    /// <param name="identifier">The tenant's identifier.</param>
    /// <returns>The tenant's origin.</returns>
    public string? TenantUrl(string identifier)
        => IsEnabled
            ? _template.Replace(TenantPlaceholder, identifier.Trim().ToLowerInvariant(), StringComparison.Ordinal)
            : null;

    /// <summary>The platform origin, <c>Web:DefaultDomain</c>.</summary>
    /// <returns>The platform origin.</returns>
    public string PlatformUrl() => _defaultDomain;

    /// <summary>
    /// Parses a non-empty template into the host suffix and port its tenant hosts share.
    /// </summary>
    private static bool TryParseTemplate(string template, out Authority suffix)
    {
        suffix = default;

        var first = template.IndexOf(TenantPlaceholder, StringComparison.Ordinal);
        if (first < 0 || template.IndexOf(TenantPlaceholder, first + TenantPlaceholder.Length, StringComparison.Ordinal) >= 0)
        {
            return false;
        }

        // The placeholder must open the authority and be followed by a dot: the entire first label.
        var schemeEnd = template.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0 || first != schemeEnd + 3 || !template.AsSpan(first + TenantPlaceholder.Length).StartsWith("."))
        {
            return false;
        }

        if (!Uri.TryCreate(template.Replace(TenantPlaceholder, SampleLabel, StringComparison.Ordinal), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.HostNameType != UriHostNameType.Dns
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        if (!host.StartsWith(SampleLabel + ".", StringComparison.Ordinal) || host.Length == SampleLabel.Length + 1)
        {
            return false;
        }

        suffix = new Authority(host[(SampleLabel.Length + 1)..], uri.Port, uri.IsDefaultPort);
        return true;
    }

    /// <summary>
    /// Whether a label could be a tenant identifier: non-empty, lower-case letters, digits and hyphens.
    /// A dot here is a nested host, which names nothing.
    /// </summary>
    private static bool IsTenantLabel(string label)
        => label.Length > 0 && label.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');

    /// <summary>A host name and port, with whether the port is its scheme's default.</summary>
    private readonly record struct Authority(string Host, int Port, bool IsDefaultPort)
    {
        public bool MatchesPort(int? port) => port is { } explicitPort ? explicitPort == Port : IsDefaultPort;

        public bool Matches(string host, int? port) => string.Equals(Host, host, StringComparison.Ordinal) && MatchesPort(port);
    }
}
