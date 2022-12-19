using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using EasyForNet.Domain.Entities;
using EasyForNet.Domain.Entities.Audit;
using EasyForNet.Entities;
using EasyForNet.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Data.Context;

// Uses all the built-in Identity types
// Uses `string` as the key type
public abstract class EfnDbContext : EfnDbContext<IdentityUser>
{
    protected EfnDbContext(DbContextOptions dbContextOptions, ICurrentUser currentUser) : base(dbContextOptions, currentUser)
    {
    }
}

// Uses the built-in Identity types except with a custom User type
// Uses `string` as the key type
public abstract class EfnDbContext<TUser> : EfnDbContext<TUser, IdentityRole, string>
    where TUser : IdentityUser
{
    public EfnDbContext(DbContextOptions dbContextOptions, ICurrentUser currentUser) : base(dbContextOptions, currentUser)
    {
    }
}

// Uses the built-in Identity types except with custom User and Role types
// The key type is defined by TKey
public abstract class EfnDbContext<TUser, TRole, TKey> : EfnDbContext<
    TUser, TRole, TKey, IdentityUserClaim<TKey>, IdentityUserRole<TKey>,
    IdentityUserLogin<TKey>, IdentityRoleClaim<TKey>, IdentityUserToken<TKey>>
        where TUser : IdentityUser<TKey>
        where TRole : IdentityRole<TKey>
        where TKey : IEquatable<TKey>
{
    protected EfnDbContext(DbContextOptions dbContextOptions, ICurrentUser currentUser) : base(dbContextOptions, currentUser)
    {
    }
}

// No built-in Identity types are used; all are specified by generic arguments
// The key type is defined by TKey
public abstract class EfnDbContext<TUser, TRole, TKey, TUserClaim, TUserRole, TUserLogin, TRoleClaim,
    TUserToken>
    : IdentityDbContext<TUser, TRole, TKey, TUserClaim, TUserRole, TUserLogin, TRoleClaim, TUserToken>
         where TUser : IdentityUser<TKey>
         where TRole : IdentityRole<TKey>
         where TKey : IEquatable<TKey>
         where TUserClaim : IdentityUserClaim<TKey>
         where TUserRole : IdentityUserRole<TKey>
         where TUserLogin : IdentityUserLogin<TKey>
         where TRoleClaim : IdentityRoleClaim<TKey>
         where TUserToken : IdentityUserToken<TKey>
{
    private readonly ICurrentUser _currentUser;

    #region Constructors

    protected EfnDbContext(DbContextOptions dbContextOptions, ICurrentUser currentUser)
        : base(dbContextOptions)
    {
        _currentUser = currentUser;
    }

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        SoftDeleteFilter(modelBuilder);

        IdentityEntities(modelBuilder);

        SettingConfiguration(modelBuilder.Entity<EfnSetting>());
    }

    public override int SaveChanges()
    {
        OnBeforeSaving();

        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        OnBeforeSaving();

        return base.SaveChangesAsync(cancellationToken);
    }

    #endregion

    #region Helpers

    private void SoftDeleteFilter(ModelBuilder modelBuilder)
    {
        var softDeleteEntities =
                    GetType().Assembly.GetConcreteTypes()
                    .Where(type => typeof(ISoftDeleteEntity).IsAssignableFrom(type));

        foreach (var softDeleteEntity in softDeleteEntities)
        {
            modelBuilder.Entity(softDeleteEntity)
                .HasQueryFilter(NotDeletedExp(softDeleteEntity));
        }
    }

    private LambdaExpression NotDeletedExp(Type type)
    {
        // we should generate:  e => e.IsDeleted == false
        // or: e => !e.IsDeleted

        // e =>
        var parameter = Expression.Parameter(type, "e");

        // false
        var falseConstant = Expression.Constant(false);

        // e.IsDeleted
        var propertyAccess = Expression.PropertyOrField(parameter, nameof(ISoftDeleteEntity.IsDeleted));

        // e.IsDeleted == false
        var equalExpression = Expression.Equal(propertyAccess, falseConstant);

        // e => e.IsDeleted == false
        var lambda = Expression.Lambda(equalExpression, parameter);

        return lambda;
    }
    
    private void OnBeforeSaving()
    {
        var entries = ChangeTracker.Entries();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added && entry.Entity is ICreateAuditEntity createAuditable)
            {
                var now = DateTime.UtcNow;
                var user = _currentUser?.Username;

                createAuditable.CreatedAt = now;
                createAuditable.CreatedBy = user;
            }
            else if (entry.State == EntityState.Modified && entry.Entity is IUpdateAuditEntity updateAuditable)
            {
                var now = DateTime.UtcNow;
                var user = _currentUser?.Username;

                if (entry.Entity is ICreateAuditEntity)
                {
                    entry.Property(nameof(ICreateAuditEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(ICreateAuditEntity.CreatedBy)).IsModified = false;
                }
                updateAuditable.UpdatedAt = now;
                updateAuditable.UpdatedBy = user;
            }
        }
    }

    private void SettingConfiguration(EntityTypeBuilder<EfnSetting> settingBuilder)
    {
        settingBuilder
            .ToTable("EfnSettings")
            .HasIndex(e => e.Key)
            .IsUnique();
    }

    private void IdentityEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TUser>().ToTable("EfnUsers");
        modelBuilder.Entity<TRole>().ToTable("EfnRoles");
        modelBuilder.Entity<TUserRole>().ToTable("EfnUsersRoles");
        modelBuilder.Entity<TUserClaim>().ToTable("EfnUserClaims");
        modelBuilder.Entity<TRoleClaim>().ToTable("EfnRoleClaims");
        modelBuilder.Entity<TUserToken>().ToTable("EfnUserTokens");
        modelBuilder.Entity<TUserLogin>().ToTable("EfnUserLogins");
    }

    #endregion
}