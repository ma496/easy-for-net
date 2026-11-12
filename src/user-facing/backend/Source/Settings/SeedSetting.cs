namespace Backend.Settings;

/// <summary>
/// Strongly-typed options bound from the <c>Seed</c> configuration section: the password
/// <c>DataSeeder</c> gives the administrator account it creates on first start.
/// </summary>
/// <remarks>
/// The password is applied only when the account is created, so changing the value here later changes
/// nothing for an account that already exists.
/// </remarks>
public class SeedSetting
{
    /// <summary>
    /// The password the administrator is seeded with in Development and Testing. It is documented
    /// in <c>CLAUDE.md</c> so a browser test can sign in, which is why startup refuses it everywhere else.
    /// </summary>
    public const string LocalPassword = "Admin#123";

    /// <summary>The password of the administrator account (<c>admin</c>).</summary>
    public string AdminPassword { get; set; } = null!;
}
