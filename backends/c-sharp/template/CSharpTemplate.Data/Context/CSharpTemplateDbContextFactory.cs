using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CSharpTemplate.Data.Context;

public class CSharpTemplateDbContextFactory : IDesignTimeDbContextFactory<CSharpTemplateDbContext>
{
    public CSharpTemplateDbContext CreateDbContext(string[] args)
    {
        var path = Directory.GetCurrentDirectory();

        IConfigurationBuilder builder =
            new ConfigurationBuilder()
                .SetBasePath(path)
                .AddJsonFile("appsettings.json");

        IConfigurationRoot config = builder.Build();

        var connectionString = config.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Could not find connection string named 'DefaultConnection'");
        }

        var dbContextOptionsBuilder = new DbContextOptionsBuilder<CSharpTemplateDbContext>()
            .UseSqlServer(connectionString);

        return new CSharpTemplateDbContext(dbContextOptionsBuilder.Options, null);
    }
}