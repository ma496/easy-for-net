using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using EasyForNet.Domain.Entities;
using EasyForNet.Domain.Entities.Audit;
using EasyForNet.Entities;
using EasyForNet.Extensions;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Data.Context;

public abstract class DbContextBase : DbContext
{
    private readonly ICurrentUser _currentUser;

    #region Constructors

    protected DbContextBase(DbContextOptions dbContextOptions, ICurrentUser currentUser)
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

        SettingConfiguration(modelBuilder);
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
            if (entry.Entity is IAuditEntity auditable)
            {
                var now = DateTime.UtcNow;
                var user = _currentUser?.Username;
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditable.CreatedAt = now;
                        auditable.CreatedBy = user;
                        auditable.UpdatedAt = now;
                        auditable.UpdatedBy = user;
                        break;

                    case EntityState.Modified:
                        entry.Property(nameof(auditable.CreatedAt)).IsModified = false;
                        entry.Property(nameof(auditable.CreatedBy)).IsModified = false;
                        auditable.UpdatedAt = now;
                        auditable.UpdatedBy = user;
                        break;
                }
            }
        }
    }

    private void SettingConfiguration(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EfnSetting>()
            .ToTable("EfnSettings")
            .HasIndex(e => e.Key)
            .IsUnique();
    }

    #endregion
}