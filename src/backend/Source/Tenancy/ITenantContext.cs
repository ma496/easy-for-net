namespace Backend.Tenancy;

/// <summary>
/// Ambient accessor for the tenant the current unit of work acts for. Exactly one scope is active at
/// a time and it is always one of three states: resolved to a tenant, resolved to platform scope
/// (no tenant, so reads and writes are attributed to the platform rather than to a tenant), or
/// unresolved. Unresolved is deliberately distinct from platform scope: work that runs outside a user
/// request, such as a scheduled or queued job, starts unresolved and must name the tenant it acts for
/// with <see cref="BeginTenant"/>, so a caller that forgot to do so fails loudly instead of quietly
/// reading no rows or writing an unattributed one.
/// </summary>
/// <remarks>
/// One instance carries one scope, so it belongs to one unit of work: dispose the handles a
/// <c>Begin</c> method hands back in the reverse of the order they were taken, and never open a
/// scope on an instance that a concurrent branch is also using. Work that fans out over several
/// tenants at once gives each branch its own instance rather than sharing one, otherwise a branch
/// reads and writes whichever tenant another branch happened to establish last.
/// </remarks>
public interface ITenantContext
{
    /// <summary>
    /// Gets the active tenant's identifier, or <see langword="null"/> when the active scope is
    /// platform scope. Throws <see cref="TenantScopeNotEstablishedException"/> when no scope has been
    /// established, so that a tenant-scoped read or write fails rather than silently falling back to
    /// every tenant's rows or to none.
    /// </summary>
    /// <exception cref="TenantScopeNotEstablishedException">No scope has been established.</exception>
    Guid? CurrentTenantId { get; }

    /// <summary>
    /// Gets a value indicating whether a scope has been established, whether that scope is a tenant or
    /// platform scope. This is the only way to ask the question without throwing, and is what callers
    /// that refuse a request rather than fail it - the tenant pre-processor above all - should read.
    /// </summary>
    bool IsResolved { get; }

    /// <summary>
    /// Makes <paramref name="tenantId"/> the active tenant until the returned handle is disposed, at
    /// which point the previously active scope is restored. This is how work outside a user request
    /// supplies the tenant it acts for explicitly.
    /// </summary>
    /// <param name="tenantId">The tenant to act for; must not be <see cref="Guid.Empty"/>.</param>
    /// <returns>A handle that restores the previous scope when disposed.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is <see cref="Guid.Empty"/>.</exception>
    IDisposable BeginTenant(Guid tenantId);

    /// <summary>
    /// Establishes platform scope - resolved, but attributed to no tenant - until the returned handle
    /// is disposed. Reads see the rows that belong to the platform rather than to a tenant, and writes
    /// are persisted without a tenant attribution.
    /// </summary>
    /// <returns>A handle that restores the previous scope when disposed.</returns>
    IDisposable BeginPlatformScope();

    /// <summary>
    /// Clears the active scope until the returned handle is disposed, returning the context to the
    /// state work outside a user request starts in. Tenant-scoped reads and writes fail while it is in
    /// force unless they opt out of tenant restriction explicitly.
    /// </summary>
    /// <returns>A handle that restores the previous scope when disposed.</returns>
    IDisposable BeginUnscoped();
}