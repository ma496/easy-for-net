namespace Backend.Tests.Seeder;

using Backend.Features.Tenancy.Core;

/// <summary>
/// Stores tenant IDs created during test data seeding for use across test classes.
/// </summary>
public static class TestTenants
{
    /// <summary>
    /// The system-created bootstrap tenant the migration and <c>DataSeeder</c> always produce.
    /// Recognised by its fixed id rather than created here, so it is known before seeding runs.
    /// </summary>
    public static Guid BootstrapTenantId { get; private set; } = TenancyConstants.BootstrapTenantId;

    /// <summary>
    /// A second seeded tenant, so "another tenant" exists for read-only assertions without every
    /// test having to provision two of its own.
    /// </summary>
    public static Guid SecondTenantId { get; private set; } = default;

    /// <summary>
    /// Sets the tenant IDs after they have been created in the database during seeding.
    /// </summary>
    public static void SetTenantIds(Guid bootstrapTenantId, Guid secondTenantId)
    {
        BootstrapTenantId = bootstrapTenantId;
        SecondTenantId = secondTenantId;
    }
}
