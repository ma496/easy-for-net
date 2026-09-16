namespace Backend.Tests.Seeder;

/// <summary>
/// Stores user IDs created during test data seeding for use across test classes.
/// </summary>
public static class TestUsers
{
    public const string DefaultPassword = "Test#123";

    public static Guid AdminUserId { get; private set; } = default;
    public static Guid TestUserId { get; private set; } = default;
    public static Guid TestOneUserId { get; private set; } = default;
    public static Guid TestTwoUserId { get; private set; } = default;

    /// <summary>
    /// <c>limited</c> - a member of the bootstrap tenant holding
    /// <see cref="TestRoles.LimitedTenantRoleId"/> only. Proves a permission gate turns a caller
    /// away rather than passing everyone the way an all-permission seeded role would (AC-088).
    /// </summary>
    public static Guid LimitedUserId { get; private set; } = default;

    /// <summary>
    /// <c>nomember</c> - an active account with <b>no</b> membership at all. The AC-050/AC-122
    /// path: every tenant-scoped call is refused with an explanation rather than an empty page.
    /// </summary>
    public static Guid NoMembershipUserId { get; private set; } = default;

    /// <summary>
    /// <c>dual</c> - an active membership in <b>both</b> seeded tenants: administrator in
    /// <see cref="TestTenants.SecondTenantId"/> and <see cref="TestRoles.LimitedTenantRoleId"/> in
    /// the bootstrap tenant. The only seeded account that exercises sign-in with no active tenant,
    /// because AC-123 auto-selects only when exactly one active membership exists (AC-140/AC-149).
    /// </summary>
    public static Guid DualTenantUserId { get; private set; } = default;

    /// <summary>
    /// Sets the user IDs after they have been created in the database during seeding.
    /// </summary>
    public static void SetUserIds(Guid adminUserId, Guid testUserId, Guid testOneUserId, Guid testTwoUserId)
    {
        AdminUserId = adminUserId;
        TestUserId = testUserId;
        TestOneUserId = testOneUserId;
        TestTwoUserId = testTwoUserId;
    }

    /// <summary>
    /// Sets the tenancy user IDs after they have been created in the database during seeding.
    /// </summary>
    public static void SetTenantUserIds(Guid limitedUserId, Guid noMembershipUserId, Guid dualTenantUserId)
    {
        LimitedUserId = limitedUserId;
        NoMembershipUserId = noMembershipUserId;
        DualTenantUserId = dualTenantUserId;
    }
}
