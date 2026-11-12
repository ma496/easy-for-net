namespace Backend.Tests.Seeder;

/// <summary>
/// Stores role IDs created during test data seeding for use across test classes.
/// </summary>
public static class TestRoles
{
    /// <summary>The system-created <c>Admin</c> role <c>DataSeeder</c> keeps holding every permission.</summary>
    public static Guid AdminRoleId { get; private set; } = default;

    /// <summary>The <c>User</c> role <c>DataSeeder</c> creates with no permissions - the sign-up default.</summary>
    public static Guid UserRoleId { get; private set; } = default;

    public static Guid TestRoleId { get; private set; } = default;
    public static Guid TestOneRoleId { get; private set; } = default;
    public static Guid TestTwoRoleId { get; private set; } = default;

    /// <summary>
    /// A role holding exactly one permission (<see cref="Backend.Permissions.Allow.Seo_View"/>). Every
    /// other seeded test role holds every permission, which is precisely why the permission gate tests
    /// need a purpose-built role: only a single-permission role can prove an endpoint's
    /// <c>Permissions(...)</c> declaration is what turns a caller away.
    /// </summary>
    public static Guid LimitedRoleId { get; private set; } = default;

    /// <summary>
    /// Sets the role IDs after they have been created in the database during seeding.
    /// </summary>
    public static void SetRoleIds(Guid adminRoleId, Guid userRoleId, Guid testRoleId, Guid testOneRoleId, Guid testTwoRoleId, Guid limitedRoleId)
    {
        AdminRoleId = adminRoleId;
        UserRoleId = userRoleId;
        TestRoleId = testRoleId;
        TestOneRoleId = testOneRoleId;
        TestTwoRoleId = testTwoRoleId;
        LimitedRoleId = limitedRoleId;
    }
}
