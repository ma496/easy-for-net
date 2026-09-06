namespace Backend.Features.Identity.Core;

using System.Security.Cryptography;
using Backend.Attributes;

/// <summary>
/// Hashes and verifies user passwords using a salted PBKDF2 scheme with constant-time comparison.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string hashedPassword, string providedPassword);
    bool NeedsRehash(string hashedPassword);
}

/// <summary>
/// Default <see cref="IPasswordHasher"/> implementation backed by <c>Rfc2898DeriveBytes</c> with a per-password random salt.
/// </summary>
[NoDirectUse]
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128 bit
    private const int KeySize = 32;  // 256 bit
    private const int Iterations = 600000;
    private const int LegacyIterations = 100000;
    private const string Algorithm = "pbkdf2-sha256";
    private static readonly HashAlgorithmName _hashAlgorithm = HashAlgorithmName.SHA256;

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            _hashAlgorithm,
            KeySize);

        return $"{Algorithm}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        try
        {
            var parts = hashedPassword.Split('.');
            var isLegacy = parts.Length == 2;
            if (!isLegacy && (parts.Length != 4 || parts[0] != Algorithm || !int.TryParse(parts[1], out _)))
            {
                return false;
            }

            var iterations = isLegacy ? LegacyIterations : int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[isLegacy ? 0 : 2]);
            var hash = Convert.FromBase64String(parts[isLegacy ? 1 : 3]);

            var providedHash = Rfc2898DeriveBytes.Pbkdf2(
                providedPassword,
                salt,
                iterations,
                _hashAlgorithm,
                KeySize);

            return CryptographicOperations.FixedTimeEquals(hash, providedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool NeedsRehash(string hashedPassword)
    {
        return !hashedPassword.StartsWith($"{Algorithm}.{Iterations}.", StringComparison.Ordinal);
    }
}
