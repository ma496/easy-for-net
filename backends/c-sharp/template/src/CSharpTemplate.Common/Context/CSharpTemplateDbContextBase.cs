using CSharpTemplate.Common.Identity.Entities;
using EasyForNet;
using EasyForNet.EntityFramework.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace CSharpTemplate.Common.Context;

public abstract class CSharpTemplateDbContextBase : EfnDbContext
{
#pragma warning disable CS8618
    protected CSharpTemplateDbContextBase(DbContextOptions dbContextOptions, ICurrentUser? currentUser)
#pragma warning restore CS8618
        : base(dbContextOptions, currentUser)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserRole>()
            .HasKey(e => new { e.UserId, e.RoleId });
        
        modelBuilder.Entity<RolePermission>()
            .HasKey(e => new { e.RoleId, e.PermissionId });

        CreateRoles(modelBuilder);
        CreateUsers(modelBuilder);
    }
    
    private void CreateRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                Name = "Admin",
                
            }
        );
    }

    private void CreateUsers(ModelBuilder modelBuilder)
    {
        throw new NotImplementedException();
    }
}
