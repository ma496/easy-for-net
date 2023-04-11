using CSharpTemplate.App.Data;

namespace CSharpTemplate.Api.Extensions;

public static class DbMigrateIntegration
{
    public static async Task DbMigrateAsync(this WebApplication app)
    {
        var dbManager = app.Services.GetRequiredService<CSharpTemplateDbManager>();
        await dbManager.MigrateAsync();
    }
}