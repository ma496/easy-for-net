using System.Linq.Expressions;
using Backend.ShareData.Entities.Base;
using Backend.Features.FileManagement.Core.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Localization.Core.Entities;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Seo.Core.Entities;
using Backend.Features.Settings.Core.Entities;

namespace Backend.ShareData;

/// <summary>
/// Central EF Core <see cref="DbContext"/> for the backend. Exposes the
/// application's entity sets and applies cross-cutting concerns such as
/// the soft-delete query filter, audit field population, and property
/// normalization on every save.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options,
                          ICurrentUserService currentUserService)
    : DbContext(options)
{
    /// <summary>
    /// Key of the query filter that keeps soft-deleted rows out of every query.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuthToken> AuthTokens => Set<AuthToken>();
    public DbSet<Token> Tokens => Set<Token>();

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationVisit> NotificationVisits => Set<NotificationVisit>();
    public DbSet<NotificationReadCursor> NotificationReadCursors => Set<NotificationReadCursor>();

    // FileManagement
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    // Localization
    public DbSet<LocalizationText> LocalizationTexts => Set<LocalizationText>();
    public DbSet<LanguageSetting> LanguageSettings => Set<LanguageSetting>();

    // Settings
    public DbSet<SettingValue> SettingValues => Set<SettingValue>();

    // Seo
    public DbSet<SeoPage> SeoPages => Set<SeoPage>();

    /// <summary>
    /// Configures the EF Core model by applying configuration classes from the
    /// current assembly and registering the global soft-delete query filter.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        SoftDeleteFilter(modelBuilder);
    }

    private static void SoftDeleteFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ISoftDelete).IsAssignableFrom(t.ClrType)))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeletedProp = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
            var filter = Expression.Lambda(
                Expression.Equal(isDeletedProp, Expression.Constant(false)),
                parameter);

            modelBuilder.Entity(entityType.ClrType)
                        .HasQueryFilter(SoftDeleteFilterKey, filter);
        }
    }

    /// <summary>
    /// Synchronous entry point for persisting changes. Applies soft-delete
    /// rules, normalizes properties, and stamps the audit fields with the
    /// current user and timestamp before delegating to the base implementation.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    public override int SaveChanges()
    {
        PrepareForSave();
        return base.SaveChanges();
    }

    /// <summary>
    /// Asynchronous entry point for persisting changes. Applies soft-delete
    /// rules, normalizes properties, and stamps the audit fields with the
    /// current user and timestamp before delegating to the base implementation.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the save operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        PrepareForSave();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void PrepareForSave()
    {
        ApplySoftDeleteRules();
        NormalizeProperties();

        var currentUserId = currentUserService.GetCurrentUserId();
        var entries = ChangeTracker
            .Entries()
            .Where(e => (e.Entity is ICreatableEntity || e.Entity is IUpdatableEntity) && (
                e.State == EntityState.Added ||
                e.State == EntityState.Modified));

        foreach (var entityEntry in entries)
        {
            if (entityEntry.State == EntityState.Added)
            {
                if (entityEntry.Entity is ICreatableEntity creatableEntity)
                {
                    creatableEntity.CreatedAt = DateTime.UtcNow;
                    creatableEntity.CreatedBy = currentUserId;
                }

                if (entityEntry.Entity is IUpdatableEntity updatableEntity)
                {
                    updatableEntity.UpdatedAt = DateTime.UtcNow;
                }
            }
            else if (entityEntry is { State: EntityState.Modified, Entity: IUpdatableEntity updatableEntity })
            {
                updatableEntity.UpdatedAt = DateTime.UtcNow;
                updatableEntity.UpdatedBy = currentUserId;
            }
        }
    }

    private void NormalizeProperties()
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is IHasNormalizedProperties && (
                e.State == EntityState.Added ||
                e.State == EntityState.Modified));

        foreach (var entityEntry in entries)
        {
            if (entityEntry.Entity is IHasNormalizedProperties hasNormalizedProperties)
            {
                hasNormalizedProperties.NormalizeProperties();
            }
        }
    }

    private void ApplySoftDeleteRules()
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDelete>()
                                         .Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;
        }
    }
}
