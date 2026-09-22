namespace Backend.ShareData;

using System.Linq.Expressions;
using Backend.ShareData.Entities;
using Backend.ShareData.Entities.Base;
using Backend.Features.FileManagement.Core.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Core;
using Microsoft.EntityFrameworkCore.ChangeTracking;

/// <summary>
/// Central EF Core <see cref="DbContext"/> for the backend. Exposes the
/// application's entity sets and applies cross-cutting concerns such as
/// soft-delete and tenant query filters, tenant attribution, audit field
/// population, and property normalization on every save.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options,
                          ICurrentUserService currentUserService,
                          ITenantContext tenantContext)
    : DbContext(options)
{
    /// <summary>
    /// Key of the query filter that keeps soft-deleted rows out of every query. It is named so that a
    /// query may relax one restriction without losing the other.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// Key of the query filter that restricts tenant-scoped rows to the active tenant. Naming it is
    /// what lets <c>AcrossAllTenants()</c> relax tenant restriction alone, explicitly and by name,
    /// while the soft-delete filter stays in force.
    /// </summary>
    private const string TenantFilterKey = "Tenant";

    // Tenancy - the kernel every tenant-scoped set below is filtered against, so it comes first.
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

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

    // FileManagement
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    /// <summary>
    /// Gets the tenant the <c>Tenant</c> query filter restricts to. The filter reads this property
    /// rather than a value captured while the model was built, so every query - including a bulk
    /// <c>ExecuteUpdate</c> or <c>ExecuteDelete</c> statement - is restricted to the tenant that is
    /// active at the moment it runs. Reading it while no scope has been established throws, so a
    /// caller that established no tenant and opted out of nothing fails rather than reading
    /// unrestricted or empty results. The null-conditional access is for design time only, where the
    /// model is built without a tenant accessor.
    /// </summary>
    /// <exception cref="TenantScopeNotEstablishedException">No tenant scope has been established.</exception>
    public Guid? CurrentTenantId => tenantContext?.CurrentTenantId;

    /// <summary>
    /// Configures the EF Core model by applying configuration classes from the
    /// current assembly and registering the global soft-delete and tenant query
    /// filters.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        SoftDeleteFilter(modelBuilder);
        TenantFilter(modelBuilder);
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
    /// Registers the <c>Tenant</c> query filter - <c>e =&gt; e.TenantId == CurrentTenantId</c> - for
    /// every tenant-scoped entity type, so a persisted kind becomes tenant-restricted by implementing
    /// <see cref="IMayHaveTenant"/> or <see cref="IHaveTenant"/> alone and nothing here has to be
    /// edited when one is added. The filter is registered under its own key beside the soft-delete
    /// filter, so both apply to a kind subject to both and relaxing either leaves the other in force.
    /// </summary>
    /// <remarks>
    /// This is an instance method because the expression must read <see cref="CurrentTenantId"/> off
    /// this context. Referencing the context that way is what makes the tenant a per-query value
    /// instead of one tenant baked into the model the first time it was built.
    /// </remarks>
    /// <param name="modelBuilder">The builder being used to construct the model.</param>
    private void TenantFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => IsTenantScoped(t.ClrType)))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            Expression tenantIdProp = Expression.Property(parameter, nameof(IMayHaveTenant.TenantId));

            // An IHaveTenant type declares TenantId as a plain Guid, so lift it to Guid? before the
            // comparison: the active tenant is nullable, and Expression.Equal will not pair the two.
            // The lifted comparison still translates to the same SQL equality against the parameter.
            if (tenantIdProp.Type == typeof(Guid))
            {
                tenantIdProp = Expression.Convert(tenantIdProp, typeof(Guid?));
            }

            var currentTenantIdProp = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
            var filter = Expression.Lambda(
                Expression.Equal(tenantIdProp, currentTenantIdProp),
                parameter);

            modelBuilder.Entity(entityType.ClrType)
                        .HasQueryFilter(TenantFilterKey, filter);
        }
    }

    /// <summary>
    /// Whether a CLR type is subject to tenant restriction, by either marker. The two are separate
    /// interfaces rather than one inheriting the other because they differ in the very thing that
    /// distinguishes them - whether <c>TenantId</c> is nullable - so every place that treats them
    /// alike asks here.
    /// </summary>
    /// <param name="clrType">The entity type to test.</param>
    /// <returns><see langword="true"/> when the type carries a tenant of its own.</returns>
    private static bool IsTenantScoped(Type clrType)
        => typeof(IMayHaveTenant).IsAssignableFrom(clrType) || typeof(IHaveTenant).IsAssignableFrom(clrType);

    /// <summary>
    /// Synchronous entry point for persisting changes. Applies soft-delete
    /// rules, normalizes properties, attributes tenant-scoped rows to the active
    /// tenant, and stamps the audit fields with the current user and timestamp
    /// before delegating to the base implementation.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    public override int SaveChanges()
    {
        ApplySoftDeleteRules();
        NormalizeProperties();
        ApplyTenantRules();

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

        return base.SaveChanges();
    }

    /// <summary>
    /// Asynchronous entry point for persisting changes. Applies soft-delete
    /// rules, normalizes properties, attributes tenant-scoped rows to the active
    /// tenant, and stamps the audit fields with the current user and timestamp
    /// before delegating to the base implementation.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the save operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplySoftDeleteRules();
        NormalizeProperties();
        ApplyTenantRules();

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

        return await base.SaveChangesAsync(cancellationToken);
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

    /// <summary>
    /// Attributes every tenant-scoped row being written to the tenant that is active, and refuses any
    /// write whose attribution would disagree with it. Attribution comes from the active scope and
    /// never from the caller, so no request payload can decide which tenant a row lands in.
    /// </summary>
    private void ApplyTenantRules()
    {
        foreach (var entry in ChangeTracker.Entries<IMayHaveTenant>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.State == EntityState.Added)
            {
                AttributeAddedEntry(entry);
            }
            else
            {
                RefuseReattribution(entry);
            }
        }

        foreach (var entry in ChangeTracker.Entries<IHaveTenant>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.State == EntityState.Added)
            {
                AttributeAddedEntry(entry);
            }
            else
            {
                RefuseReattribution(entry);
            }
        }
    }

    /// <summary>
    /// Stamps a new tenant-scoped row with the active tenant, or refuses it. A row that carries no
    /// attribution takes the active tenant - which, in platform scope, is deliberately no tenant at
    /// all, the only way a platform-scoped row is ever written. With no scope established there is
    /// nothing to attribute the row to, so the write fails rather than storing an unattributed row.
    /// A row that already names a tenant other than the active one is a programming error and is
    /// refused as well.
    /// </summary>
    /// <param name="entry">The change-tracker entry for the row being added.</param>
    /// <exception cref="TenantScopeNotEstablishedException">
    /// The row carries no attribution and no tenant scope has been established.
    /// </exception>
    /// <exception cref="TenantAttributionException">
    /// The row names a tenant other than the active one.
    /// </exception>
    private void AttributeAddedEntry(EntityEntry<IMayHaveTenant> entry)
    {
        if (entry.Entity.TenantId is null)
        {
            if (tenantContext is not { IsResolved: true })
            {
                throw new TenantScopeNotEstablishedException();
            }

            entry.Entity.TenantId = tenantContext.CurrentTenantId;
            return;
        }

        if (tenantContext is { IsResolved: true }
            && tenantContext.CurrentTenantId is { } activeTenantId
            && entry.Entity.TenantId != activeTenantId)
        {
            throw new TenantAttributionException();
        }
    }

    /// <summary>
    /// Stamps a new row of a kind that must always name a tenant, or refuses it. The nullable
    /// counterpart above can fall back on platform scope; this one cannot, because there is no
    /// tenantless row to write. An unattributed row therefore needs an active tenant to take, and
    /// both an unresolved scope and platform scope leave it without one, so the write fails instead
    /// of inventing an attribution. A row that already names a tenant other than the active one is
    /// refused exactly as it is for a nullable kind.
    /// </summary>
    /// <param name="entry">The change-tracker entry for the row being added.</param>
    /// <exception cref="TenantScopeNotEstablishedException">
    /// The row carries no attribution and there is no active tenant to attribute it to.
    /// </exception>
    /// <exception cref="TenantAttributionException">
    /// The row names a tenant other than the active one.
    /// </exception>
    private void AttributeAddedEntry(EntityEntry<IHaveTenant> entry)
    {
        if (entry.Entity.TenantId == Guid.Empty)
        {
            if (tenantContext is not { IsResolved: true })
            {
                throw new TenantScopeNotEstablishedException();
            }

            if (tenantContext.CurrentTenantId is not { } tenantId)
            {
                throw new TenantScopeNotEstablishedException(
                    "This record must belong to a tenant, but the active scope is the platform itself.");
            }

            entry.Entity.TenantId = tenantId;
            return;
        }

        if (tenantContext is { IsResolved: true }
            && tenantContext.CurrentTenantId is { } activeTenantId
            && entry.Entity.TenantId != activeTenantId)
        {
            throw new TenantAttributionException();
        }
    }

    /// <summary>
    /// Refuses to move an existing tenant-scoped row from one tenant to another. A row's tenant is
    /// decided once, when it is created, so a changed attribution is a programming error rather than
    /// an edit.
    /// </summary>
    /// <param name="entry">The change-tracker entry for the row being modified.</param>
    /// <exception cref="TenantAttributionException">The row's tenant attribution has been changed.</exception>
    private static void RefuseReattribution(EntityEntry entry)
    {
        var tenantId = entry.Property(nameof(IMayHaveTenant.TenantId));

        if (!Equals(tenantId.OriginalValue, tenantId.CurrentValue))
        {
            throw new TenantAttributionException();
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
