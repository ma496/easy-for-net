namespace Backend.Tenancy;

/// <summary>
/// Fixed identifiers shared by the migration, the data seeder and the tests.
/// </summary>
public static class TenancyConstants
{
    /// <summary>
    /// Identity of the system-created bootstrap tenant. Fixed so the migration, the seeder and the
    /// tests all name the same row instead of re-deriving it.
    /// </summary>
    public static readonly Guid BootstrapTenantId = new("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Display name of the system-created bootstrap tenant.
    /// </summary>
    public const string BootstrapTenantName = "Default";

    /// <summary>
    /// Stable, url-safe identifier of the system-created bootstrap tenant.
    /// </summary>
    public const string BootstrapTenantIdentifier = "default";
}