using CSharpTemplate.Common.Context;
using EasyForNet;
using Microsoft.EntityFrameworkCore;

namespace CSharpTemplate.Data.Context;

public class CSharpTemplateDbContext : CSharpTemplateDbContextBase
{
    public CSharpTemplateDbContext(DbContextOptions dbContextOptions, ICurrentUser? currentUser) 
        : base(dbContextOptions, currentUser)
    {
    }
}
