using EasyForNet;
using EasyForNet.EntityFramework.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace CSharpTemplate.Data.Context;

public class CSharpTemplateDbContext : EfnDbContext
{
    public CSharpTemplateDbContext(DbContextOptions dbContextOptions, ICurrentUser? currentUser) 
        : base(dbContextOptions, currentUser)
    {
    }
}
