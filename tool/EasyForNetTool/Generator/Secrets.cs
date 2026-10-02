namespace EasyForNetTool.Generator;

using System.Security.Cryptography;

/// <summary>
/// Random values a new project is created with, so no two generated projects share a password or key.
/// </summary>
internal static class Secrets
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Alphanumerics = Upper + Lower + Digits;

    /// <summary>
    /// A random string of letters and digits only, safe inside a connection string, a Redis
    /// <c>password=</c> option and an env file without quoting.
    /// </summary>
    /// <param name="length">The number of characters.</param>
    public static string Alphanumeric(int length) => RandomNumberGenerator.GetString(Alphanumerics, length);

    /// <summary>
    /// A random account password that always holds an upper-case letter, a lower-case letter, a digit
    /// and a symbol, so it passes a password policy requiring each of them.
    /// </summary>
    /// <param name="length">The number of characters, at least 4.</param>
    public static string Password(int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 4);

        var characters = (RandomNumberGenerator.GetString(Upper, 1)
                + RandomNumberGenerator.GetString(Lower, 1)
                + RandomNumberGenerator.GetString(Digits, 1)
                + "#"
                + Alphanumeric(length - 4))
            .ToCharArray();
        RandomNumberGenerator.Shuffle(characters.AsSpan());
        return new string(characters);
    }

    /// <summary>
    /// <paramref name="byteCount"/> random bytes, base64-encoded - the form a signing key takes.
    /// </summary>
    /// <param name="byteCount">The number of random bytes.</param>
    public static string Base64(int byteCount) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount));
}
