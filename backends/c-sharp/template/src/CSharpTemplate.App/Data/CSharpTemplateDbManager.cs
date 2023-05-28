using CSharpTemplate.Common.Identity.Permissions.Provider;
using CSharpTemplate.Data.Context;
using EasyForNet.Application.Dependencies;
using EasyForNet.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CSharpTemplate.App.Data;

public class CSharpTemplateDbManager : ITransientDependency
{
    private readonly CSharpTemplateDbContext _dbContext;
    private readonly IPermissionsContext _permissionsContext;
    private readonly IPermissionsProvider _permissionsProvider;
    private readonly IEnumerable<IDataSeeder> _dataSeeders;
    private readonly ILogger<CSharpTemplateDbManager> _logger;

    public CSharpTemplateDbManager(CSharpTemplateDbContext dbContext, IPermissionsContext permissionsContext,
        IPermissionsProvider permissionsProvider, IEnumerable<IDataSeeder> dataSeeders,
        ILogger<CSharpTemplateDbManager> logger)
    {
        _dbContext = dbContext;
        _permissionsContext = permissionsContext;
        _permissionsProvider = permissionsProvider;
        _dataSeeders = dataSeeders;
        _logger = logger;
    }
    
    public async Task MigrateAsync()
    {
        _logger.LogInformation("Migrate the database.");
        await _dbContext.Database.MigrateAsync();

        _permissionsProvider.Permissions(_permissionsContext);

        _logger.LogInformation("Seed the data");
        foreach (var dataSeeder in _dataSeeders)
        {
            await dataSeeder.SeedAsync();
        }
    }
}