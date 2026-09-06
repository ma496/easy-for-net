namespace Backend.Features.Identity.Core;

/// <summary>
/// Strongly-typed configuration options for the authentication subsystem, including token lifetimes and JWT settings.
/// </summary>
public class AuthSetting
{
    public JwtSetting Jwt { get; set; } = null!;
    public int AccessTokenValidity { get; set; } // in minutes
    public int RefreshTokenValidity { get; set; } // in hours
}

/// <summary>
/// JWT signing configuration: secret key, issuer, and audience used to validate issued access tokens.
/// </summary>
public class JwtSetting
{
    public const string PlaceholderKey = "a long secret string used to sign jwts. usually set via matching environment variable.";

    public string Key { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
}
