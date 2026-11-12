namespace Backend.Extensions;

/// <summary>
/// Null-safe wrappers around the <see cref="string.IsNullOrEmpty(string?)"/>
/// and <see cref="string.IsNullOrWhiteSpace(string?)"/> helpers that allow
/// fluent calls on a possibly null <see cref="string"/>.
/// </summary>
public static class StringExtension
{
    /// <summary>Returns <c>true</c> if <paramref name="value"/> is null or an empty string.</summary>
    public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);
    /// <summary>Returns <c>true</c> if <paramref name="value"/> is null, empty, or contains only white-space characters.</summary>
    public static bool IsNullOrWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value);
}
