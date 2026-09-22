namespace Backend.Features.Tenancy.Core;

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
[AllowOutside]
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

/// <summary>
/// Default <see cref="ITenantContext"/> implementation. It is registered per scope, so an HTTP
/// request and a background job each get their own instance and neither can see the other's tenant.
/// The active scope is held in plain fields rather than in an <see cref="System.Threading.AsyncLocal{T}"/>
/// on purpose: a scope opened by a pre-processor has to stay in force for the endpoint that runs
/// after it, and an ambient value set inside an awaited call does not flow back out to its caller.
/// </summary>
[AllowOutside]
[NoDirectUse]
public sealed class TenantContext : ITenantContext
{
    private bool _isResolved;
    private Guid? _tenantId;

    /// <inheritdoc />
    public Guid? CurrentTenantId => _isResolved
        ? _tenantId
        : throw new TenantScopeNotEstablishedException();

    /// <inheritdoc />
    public bool IsResolved => _isResolved;

    /// <inheritdoc />
    public IDisposable BeginTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant scope must name the tenant it acts for.", nameof(tenantId));
        }

        return Begin(isResolved: true, tenantId: tenantId);
    }

    /// <inheritdoc />
    public IDisposable BeginPlatformScope() => Begin(isResolved: true, tenantId: null);

    /// <inheritdoc />
    public IDisposable BeginUnscoped() => Begin(isResolved: false, tenantId: null);

    /// <summary>
    /// Applies a scope and hands back the handle that restores whatever was in force before it, so
    /// that scopes nest and a job that opens one for a single tenant cannot leave it behind. Each
    /// handle remembers the state it displaced, so handles have to be disposed in the reverse of the
    /// order they were taken; disposing them out of order restores a scope that has already ended.
    /// </summary>
    private IDisposable Begin(bool isResolved, Guid? tenantId)
    {
        var restore = new TenantScope(this, _isResolved, _tenantId);
        _isResolved = isResolved;
        _tenantId = tenantId;
        return restore;
    }

    /// <summary>
    /// Handle returned by the tenant scope factory methods. It is only ever consumed as an
    /// <see cref="IDisposable"/>. It carries <see cref="NoDirectUseAttribute"/> because it holds a
    /// reference to its <see cref="TenantContext"/> owner, which is itself marked: without the
    /// attribute the architecture test would scan this type and read that reference as a direct use.
    /// </summary>
    [NoDirectUse]
    private sealed class TenantScope(TenantContext context, bool isResolved, Guid? tenantId) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            context._isResolved = isResolved;
            context._tenantId = tenantId;
        }
    }
}