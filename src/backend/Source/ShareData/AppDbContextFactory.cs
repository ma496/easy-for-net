namespace Backend.ShareData;

using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory used by the EF Core tools (for example when running
/// <c>dotnet ef migrations</c>) to construct an <see cref="AppDbContext"/>
/// without bootstrapping the full application host.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// Builds an <see cref="AppDbContext"/> using the development connection
    /// string from <c>appsettings.Development.json</c>.
    /// </summary>
    /// <param name="args">Command line arguments passed by the EF Core tools.</param>
    /// <returns>A new <see cref="AppDbContext"/> instance configured for PostgreSQL.</returns>
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));

        // Real no-op services rather than null!: the tools build the model here, and the model now
        // carries a tenant filter that reads the tenant accessor. Scaffolding a schema from scratch -
        // the very first command a newly generated project runs - must not depend on an accessor that
        // only an HTTP request can supply, and must not dereference a null one.
        return new AppDbContext(optionsBuilder.Options, new DesignTimeCurrentUserService(), new DesignTimeTenantContext());
    }

    /// <summary>
    /// Design-time <see cref="ICurrentUserService"/>. There is no signed-in user while the EF tools
    /// build the model, so every member reports the unauthenticated answer; audit stamping never runs
    /// at design time, so nothing reads these values for real.
    /// </summary>
    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public Guid? GetCurrentUserId() => null;

        public string? GetCurrentUsername() => null;

        public string? GetCurrentEmail() => null;

        public bool IsAuthenticated() => false;

        public bool IsInRole(string role) => false;

        public bool HasPermission(string permission) => false;

        public bool IsPlatform() => false;

        public IEnumerable<string> GetCurrentUserRoles() => [];

        public IEnumerable<string> GetCurrentUserPermissions() => [];
    }

    /// <summary>
    /// Design-time <see cref="ITenantContext"/>. It reports platform scope - resolved, attributed to
    /// no tenant - rather than the unresolved state the runtime context starts in, so that a model
    /// build which happens to read the tenant gets <see langword="null"/> instead of a
    /// <see cref="TenantScopeNotEstablishedException"/>. Scoping is meaningless with no unit of work
    /// to scope, so the <c>Begin</c> methods hand back a handle that restores nothing.
    /// </summary>
    private sealed class DesignTimeTenantContext : ITenantContext
    {
        /// <inheritdoc />
        public Guid? CurrentTenantId => null;

        /// <inheritdoc />
        public bool IsResolved => true;

        /// <inheritdoc />
        public IDisposable BeginTenant(Guid tenantId) => new DesignTimeScope();

        /// <inheritdoc />
        public IDisposable BeginPlatformScope() => new DesignTimeScope();

        /// <inheritdoc />
        public IDisposable BeginUnscoped() => new DesignTimeScope();

        /// <summary>
        /// Handle returned by the design-time scope factory methods. Disposing it does nothing,
        /// because the scope it stands for changed nothing.
        /// </summary>
        private sealed class DesignTimeScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
