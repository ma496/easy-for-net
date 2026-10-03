namespace Backend.Features.Tenancy.Core;

/// <summary>
/// Fixed identifiers shared by the migration, the data seeder and the tests.
/// </summary>
[AllowOutside]
public static class TenancyConstants
{
    /// <summary>
    /// Identity of the system-created bootstrap tenant. A real random identifier rather than a
    /// recognisable sequential one, so it is not guessable from the outside, but fixed here so the
    /// migration, the seeder and the tests all name the same row instead of re-deriving it.
    /// </summary>
    public static readonly Guid BootstrapTenantId = new("911014bc-67f7-4567-802f-d6e07a98ce55");

    /// <summary>
    /// Display name of the system-created bootstrap tenant.
    /// </summary>
    public const string BootstrapTenantName = "Default";

    /// <summary>
    /// Stable, url-safe identifier of the system-created bootstrap tenant.
    /// </summary>
    public const string BootstrapTenantIdentifier = "default";

    /// <summary>
    /// Identity of the edition the seeder creates and first puts the bootstrap tenant on. Fixed so the
    /// seeder recognises the row on every later start even after it has been renamed.
    /// </summary>
    public static readonly Guid DefaultEditionId = new("6728fb5d-b801-4692-9289-6de9d8a16f40");

    /// <summary>
    /// Name the default edition is created with.
    /// </summary>
    public const string DefaultEditionName = "Default";
}