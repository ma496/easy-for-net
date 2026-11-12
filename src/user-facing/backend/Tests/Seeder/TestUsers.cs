namespace Backend.Tests.Seeder;

/// <summary>
/// Stores user IDs created during test data seeding for use across test classes.
/// </summary>
public static class TestUsers
{
    public const string DefaultPassword = "Test#123";

    /// <summary>
    /// <c>admin</c> - the administrator <c>DataSeeder</c> creates, holding the <c>Admin</c> role and
    /// therefore every permission.
    /// </summary>
    public const string AdminUsername = "admin";

    /// <summary>
    /// The password <c>DataSeeder</c> gives <c>admin</c> - the <c>Seed:AdminPassword</c> setting, read
    /// once the host has started.
    /// </summary>
    public static string AdminPassword { get; private set; } = null!;

    public static Guid AdminUserId { get; private set; } = default;
    public static Guid TestUserId { get; private set; } = default;
    public static Guid TestOneUserId { get; private set; } = default;
    public static Guid TestTwoUserId { get; private set; } = default;

    /// <summary>
    /// <c>limited</c> - an account holding <see cref="TestRoles.LimitedRoleId"/> only. Proves a
    /// permission gate turns a caller away rather than passing everyone the way an all-permission seeded
    /// role would.
    /// </summary>
    public static Guid LimitedUserId { get; private set; } = default;

    /// <summary>
    /// <c>plain</c> - an account holding the seeded <c>User</c> role, which grants no permission at all:
    /// what a signed-up visitor is.
    /// </summary>
    public static Guid PlainUserId { get; private set; } = default;

    /// <summary>
    /// Sets the seeded administrator's password from the configuration <c>DataSeeder</c> read it from.
    /// </summary>
    public static void SetAdminPassword(string adminPassword)
    {
        AdminPassword = adminPassword;
    }

    /// <summary>
    /// Sets the user IDs after they have been created in the database during seeding.
    /// </summary>
    public static void SetUserIds(Guid adminUserId, Guid testUserId, Guid testOneUserId, Guid testTwoUserId, Guid limitedUserId, Guid plainUserId)
    {
        AdminUserId = adminUserId;
        TestUserId = testUserId;
        TestOneUserId = testOneUserId;
        TestTwoUserId = testTwoUserId;
        LimitedUserId = limitedUserId;
        PlainUserId = plainUserId;
    }
}
