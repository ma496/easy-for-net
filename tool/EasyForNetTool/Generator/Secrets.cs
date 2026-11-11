using System.Security.Cryptography;

namespace EasyForNetTool.Generator;

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
    /// <paramref name="byteCount"/> random bytes, base64-encoded - the form a signing key takes.
    /// </summary>
    /// <param name="byteCount">The number of random bytes.</param>
    public static string Base64(int byteCount) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount));
}
