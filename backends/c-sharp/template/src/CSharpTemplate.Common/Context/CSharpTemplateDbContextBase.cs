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

    public DbSet<AppUser> Users { get; set; }
}
