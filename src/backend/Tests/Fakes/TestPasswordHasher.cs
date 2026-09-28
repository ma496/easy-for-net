namespace Backend.Tests.Fakes;

using System.Security.Cryptography;
using Backend.Features.Identity.Core;

/// <summary>
/// The <see cref="IPasswordHasher"/> the test host uses in place of <c>PasswordHasher</c>.
/// </summary>
/// <remarks>
/// <para>
/// It is the production scheme - PBKDF2 over SHA-256, a random salt per password, the same
/// <c>pbkdf2-sha256.{iterations}.{salt}.{hash}</c> format - with the work factor turned down. The
/// production cost is deliberate and about a quarter of a second per hash, which the suite pays on
/// every sign-in and every account it creates; at several hundred of those it was the largest single
/// cost in the run.
/// </para>
/// <para>
/// Lowering it is safe because <c>PasswordHasher.VerifyPassword</c> reads the iteration count out of
/// the stored hash rather than from its own constant, so a password hashed here verifies against the
/// production hasher and the other way round. The production parameters stay pinned by
/// <c>PasswordHasherTests</c>, which constructs the real hasher directly and never sees this one.
/// </para>
/// </remarks>
public class TestPasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 1000;
    private const string Algorithm = "pbkdf2-sha256";
    private static readonly HashAlgorithmName _hashAlgorithm = HashAlgorithmName.SHA256;

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, _hashAlgorithm, KeySize);

        return $"{Algorithm}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var parts = hashedPassword.Split('.');
        if (parts.Length != 4 || parts[0] != Algorithm || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var hash = Convert.FromBase64String(parts[3]);
            var providedHash = Rfc2898DeriveBytes.Pbkdf2(providedPassword, salt, iterations, _hashAlgorithm, hash.Length);

            return CryptographicOperations.FixedTimeEquals(hash, providedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Always <see langword="false"/>. Reporting a hash as outdated would make
    /// <c>UserService.ValidatePasswordAsync</c> re-hash and save on every sign-in, which is the cost
    /// this class exists to avoid.
    /// </summary>
    public bool NeedsRehash(string hashedPassword) => false;
}
