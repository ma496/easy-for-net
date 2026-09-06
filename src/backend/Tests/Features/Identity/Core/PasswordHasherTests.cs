namespace Backend.Tests.Features.Identity.Core;

using System.Security.Cryptography;
using Backend.Features.Identity.Core;

public class PasswordHasherTests
{
    private readonly PasswordHasher _passwordHasher = new();

    [Fact]
    public void HashPassword_UsesVersionedCurrentFormat()
    {
        var hash = _passwordHasher.HashPassword("correct horse battery staple");

        hash.Should().StartWith("pbkdf2-sha256.600000.");
        _passwordHasher.VerifyPassword(hash, "correct horse battery staple").Should().BeTrue();
        _passwordHasher.VerifyPassword(hash, "incorrect password").Should().BeFalse();
        _passwordHasher.NeedsRehash(hash).Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_AcceptsLegacyHashAndRequestsRehash()
    {
        const string password = "legacy password";
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 32);
        var legacyHash = $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

        _passwordHasher.VerifyPassword(legacyHash, password).Should().BeTrue();
        _passwordHasher.NeedsRehash(legacyHash).Should().BeTrue();
    }
}
