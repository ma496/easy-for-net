using Backend.Attributes;
using Backend.Features.FileManagement.Core;

namespace Backend.Features.FileManagement;

/// <summary>
/// Feature module that registers the file management services with the DI container.
/// </summary>
[BypassNoDirectUse]
public class FileManagementFeature : IFeature
{
    public static void AddServices(IServiceCollection services, ConfigurationManager configuration)
    {
        // configure services
        services.AddScoped<IStorageProvider, LocalStorageProvider>();
        services.AddScoped<IFileService, FileService>();
    }
}
