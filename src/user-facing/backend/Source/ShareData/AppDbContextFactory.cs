using Backend.Features.Identity.Core;
using Microsoft.EntityFrameworkCore.Design;

namespace Backend.ShareData;

/// <summary>
/// Design-time factory used by the EF Core tools (for example when running
/// <c>dotnet ef migrations</c>) to construct an <see cref="AppDbContext"/>
/// without bootstrapping the full application host.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// Builds an <see cref="AppDbContext"/> using the development connection
    /// string from <c>appsettings.Development.json</c>, or from <c>appsettings.json</c> while that
    /// file does not exist yet - building the model needs no reachable database.
    /// </summary>
    /// <param name="args">Command line arguments passed by the EF Core tools.</param>
    /// <returns>A new <see cref="AppDbContext"/> instance configured for PostgreSQL.</returns>
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));

        // A real no-op service rather than null!: scaffolding a schema from scratch must not depend
        // on an accessor that only an HTTP request can supply.
        return new AppDbContext(optionsBuilder.Options, new DesignTimeCurrentUserService());
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

        public IEnumerable<string> GetCurrentUserRoles() => [];

        public IEnumerable<string> GetCurrentUserPermissions() => [];
    }
}
