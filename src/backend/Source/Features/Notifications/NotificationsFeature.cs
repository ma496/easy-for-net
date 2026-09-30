namespace Backend.Features.Notifications;

using Backend.Attributes;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Push;
using Microsoft.EntityFrameworkCore.Diagnostics;

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
            .Validate(options => options.MaxConnectionsPerUser > 0, "Notifications:MaxConnectionsPerUser must be greater than zero.")
            .ValidateOnStart();

        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationRetentionService, NotificationRetentionService>();

        // Pushing to the hub. The interceptor is one singleton added to every AppDbContext (Program.cs adds
        // each registered IInterceptor), which is how a push raised inside a transaction waits for its commit.
        // The hub itself, its options and the backplane are registered by AddNotificationHub from Program.cs.
        services.AddSingleton<NotificationCommitInterceptor>();
        services.AddSingleton<IInterceptor>(provider => provider.GetRequiredService<NotificationCommitInterceptor>());
        services.AddSingleton<INotificationHubSender, NotificationHubSender>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();
    }
}
