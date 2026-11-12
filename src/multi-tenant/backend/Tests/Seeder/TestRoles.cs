namespace Backend.Tests.Seeder;

/// <summary>
/// Stores role IDs created during test data seeding for use across test classes.
/// </summary>
public static class TestRoles
{
    public static Guid AdminRoleId { get; private set; } = default;
    public static Guid TestRoleId { get; private set; } = default;
    public static Guid TestOneRoleId { get; private set; } = default;
    public static Guid TestTwoRoleId { get; private set; } = default;

    /// <summary>
    /// A role inside the bootstrap tenant holding exactly one permission
    /// (<see cref="Backend.Permissions.Allow.Tenant_Detail"/>).
    /// Every other seeded role holds every permission, which is precisely why the permission gate tests need a
    /// purpose-built role: only a single-permission role can prove an endpoint's
    /// <c>Permissions(...)</c> declaration is what turns a caller away.
    /// </summary>
    public static Guid LimitedTenantRoleId { get; private set; } = default;

    /// <summary>
    /// The system-created administrator role of <see cref="TestTenants.SecondTenantId"/>.
    /// Lets a test assert "administrator here, nothing there" without provisioning a tenant.
    /// </summary>
    public static Guid SecondTenantAdminRoleId { get; private set; } = default;

    /// <summary>
    /// The platform <c>Admin</c> role (<c>TenantId == null</c>).
    /// </summary>
    public static Guid PlatformAdminRoleId { get; private set; } = default;

    /// <summary>
    /// Sets the role IDs after they have been created in the database during seeding.
    /// </summary>
    public static void SetRoleIds(Guid adminRoleId, Guid testRoleId, Guid testOneRoleId, Guid testTwoRoleId)
    {
        AdminRoleId = adminRoleId;
        TestRoleId = testRoleId;
        TestOneRoleId = testOneRoleId;
        TestTwoRoleId = testTwoRoleId;
    }

    /// <summary>
    /// Sets the tenancy role IDs after they have been created in the database during seeding.
    /// </summary>
    public static void SetTenantRoleIds(Guid limitedTenantRoleId, Guid secondTenantAdminRoleId, Guid platformAdminRoleId)
    {
        LimitedTenantRoleId = limitedTenantRoleId;
        SecondTenantAdminRoleId = secondTenantAdminRoleId;
        PlatformAdminRoleId = platformAdminRoleId;
    }
}
