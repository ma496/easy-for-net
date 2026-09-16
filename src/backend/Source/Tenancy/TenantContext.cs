namespace Backend.Tenancy;

/// <summary>
/// Default <see cref="ITenantContext"/> implementation. It is registered per scope, so an HTTP
/// request and a background job each get their own instance and neither can see the other's tenant.
/// The active scope is held in plain fields rather than in an <see cref="System.Threading.AsyncLocal{T}"/>
/// on purpose: a scope opened by a pre-processor has to stay in force for the endpoint that runs
/// after it, and an ambient value set inside an awaited call does not flow back out to its caller.
/// </summary>
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