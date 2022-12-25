using CSharpTemplate.Common.Identity.Entities;
using EasyForNet;
using EasyForNet.EntityFramework.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace CSharpTemplate.Common.Context;

public abstract class CSharpTemplateDbContextBase : EfnDbContext
{
    protected CSharpTemplateDbContextBase(DbContextOptions dbContextOptions, ICurrentUser? currentUser) : base(dbContextOptions, currentUser)
    {
    }

    public DbSet<AppUser>? Users { get; set; }
}
