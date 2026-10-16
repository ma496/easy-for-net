namespace Backend.Features.Tenancy.Core.Hosting;

/// <summary>
/// What a request's host names: nothing (resolution is off, or the host is not one of ours), the
/// platform, or one tenant.
/// </summary>
[AllowOutside]
public enum TenantHostKind
{
    /// <summary>Host resolution is off, or the host is neither the platform host nor a tenant host.</summary>
    None,

    /// <summary>The platform host - <c>Web:DefaultDomain</c>.</summary>
    Platform,

    /// <summary>A tenant host - <c>Web:TenantOriginTemplate</c> with a tenant's identifier filled in.</summary>
    Tenant,
}

/// <summary>
/// The answer <see cref="ITenantHostResolver"/> gives for one request. The host chooses, it never
/// grants: a tenant host names which tenant the caller came to, and the session - or, at sign-in, the
/// membership - decides whether the caller may act there.
/// </summary>
/// <param name="Kind">What the host names.</param>
/// <param name="Identifier">
/// For <see cref="TenantHostKind.Tenant"/>, the tenant identifier the host's first label carries, in
/// lower case - the form a tenant's normalized identifier is stored in; otherwise <see langword="null"/>.
/// </param>
[AllowOutside]
public readonly record struct TenantHost(TenantHostKind Kind, string? Identifier)
{
    /// <summary>Resolution is off, or the host names nothing.</summary>
    public static TenantHost None => new(TenantHostKind.None, null);

    /// <summary>The platform host.</summary>
    public static TenantHost Platform => new(TenantHostKind.Platform, null);

    /// <summary>A tenant host naming this identifier.</summary>
    /// <param name="identifier">The tenant identifier, lower case.</param>
    /// <returns>The resolution.</returns>
    public static TenantHost ForTenant(string identifier) => new(TenantHostKind.Tenant, identifier);

    /// <summary>Whether the host names anything at all - the platform or a tenant.</summary>
    public bool IsResolved => Kind != TenantHostKind.None;
}
