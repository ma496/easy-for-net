using CSharpTemplate.Data.Entities;
using EasyForNet;
using EasyForNet.EntityFramework.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace CSharpTemplate.Data.Context;

public class CSharpTemplateDbContext : EfnDbContext<AppUser, AppRole, string>
{
    public CSharpTemplateDbContext(DbContextOptions dbContextOptions, ICurrentUser currentUser) 
        : base(dbContextOptions, currentUser)
    {
    }
}
