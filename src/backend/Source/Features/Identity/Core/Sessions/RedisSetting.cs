namespace Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Strongly-typed configuration for the Redis deployment the session store lives in. The connection
/// string is <c>ConnectionStrings:Redis</c>; this section names what is shared between applications
/// using one Redis instance.
/// </summary>
public class RedisSetting
{
    /// <summary>
    /// Gets or sets the prefix every key this application writes starts with, so several applications
    /// (or several deployments of one) can share a Redis instance without reading each other's sessions.
    /// </summary>
    public string InstanceName { get; set; } = string.Empty;
}
