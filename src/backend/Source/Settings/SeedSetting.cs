namespace Backend.Settings;

/// <summary>
/// Strongly-typed options bound from the <c>Seed</c> configuration section: the passwords
/// <c>DataSeeder</c> gives the two administrator accounts it creates on first start.
/// </summary>
/// <remarks>
/// A password is applied only when its account is created, so changing a value here later changes
/// nothing for an account that already exists.
/// </remarks>
public class SeedSetting
{
    /// <summary>The password of the platform administrator account (<c>admin</c>).</summary>
    public string PlatformAdminPassword { get; set; } = null!;
    /// <summary>The password of the bootstrap tenant's administrator account (<c>tenantadmin</c>).</summary>
    public string TenantAdminPassword { get; set; } = null!;
}
