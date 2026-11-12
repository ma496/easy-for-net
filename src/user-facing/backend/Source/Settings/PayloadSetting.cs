namespace Backend.Settings;

/// <summary>
/// Strongly-typed options bound from the <c>Payload</c> configuration
/// section that define the request body size limits enforced by the
/// application.
/// </summary>
public class PayloadSetting
{
    /// <summary>
    /// Maximum allowed request body size in bytes. Defaults to 25 MB.
    /// </summary>
    public long MaximumSize { get; set; } = 25 * 1024 * 1024; // 25 MB
}