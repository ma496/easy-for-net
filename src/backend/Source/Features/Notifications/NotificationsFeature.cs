namespace Backend.Features.Notifications;

using Backend.Attributes;
using Backend.Features.Notifications.Core;

/// <summary>
/// Feature module that registers the notifications services with the DI container.
/// </summary>
[BypassNoDirectUse]
public class NotificationsFeature : IFeature
{
    public static void AddServices(IServiceCollection services, ConfigurationManager configuration)
    {
        services.AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.SectionName))
            .Validate(options => options.RetentionDays > 0, "Notifications:RetentionDays must be greater than zero.")
            .ValidateOnStart();

        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationRetentionService, NotificationRetentionService>();
    }
}
