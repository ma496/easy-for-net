using System;
using System.Threading;
using System.Threading.Tasks;
using EasyForNet.Domain.Entities.Audit;
using Microsoft.EntityFrameworkCore;

namespace EasyForNet.EntityFramework.Data.Context
{
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

        private void OnBeforeSaving()
        {
            var entries = ChangeTracker.Entries();
            foreach (var entry in entries)
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

        #endregion
    }
}