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
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EasyForNet.EntityFramework.Data.Context;

public abstract class EfnDbContext : DbContext
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

        SettingConfiguration(modelBuilder.Entity<EfnSetting>());

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
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

    #endregion
}