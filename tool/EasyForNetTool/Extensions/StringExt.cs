using System.Text.Json;

namespace EasyForNetTool.Extensions;

/// <summary>
/// Provides extension methods for string manipulation, including case conversions.
/// </summary>
public static class StringExt
{
    /// <summary>
    /// Converts a string to camelCase using JSON naming policy.
    /// </summary>
    public static string ToCamelCase(this string value)
    {
        return JsonNamingPolicy.CamelCase.ConvertName(value);
    }

    /// <summary>
    /// Converts a string to PascalCase by uppercasing the first character of the camelCase result.
    /// </summary>
    public static string ToPascalCase(this string value)
    {
        var camelCase = ToCamelCase(value);
        if (camelCase.Length > 0)
            return char.ToUpper(camelCase[0]) + camelCase.Substring(1);
        return camelCase;
    }

    /// <summary>
    /// Converts a string to kebab-case by inserting hyphens before uppercase letters and lowercasing.
    /// </summary>
    public static string ToKebabCase(this string value)
    {
        return string.Concat(value.Select((x, i) => i > 0 && char.IsUpper(x) ? "-" + char.ToLowerInvariant(x) : char.ToLowerInvariant(x).ToString())).ToLowerInvariant();
    }

    /// <summary>
    /// Lowercases the first character of the string.
    /// </summary>
    public static string ToLowerFirst(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;
        return char.ToLowerInvariant(str[0]) + str[1..];
    }

    /// <summary>
    /// Splits the string by the given separator and returns the last element, or the original string if the separator is not found.
    /// </summary>
    public static string SplitReturnLast(this string str, string separator)
    {
        return !string.IsNullOrWhiteSpace(str) && str.Contains(separator) ? str.Split(separator).Last() : str;
    }

    /// <summary>
    /// Uppercases the first character of the string.
    /// </summary>
    public static string Capitalize(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return str;
        return char.ToUpper(str[0]) + str[1..];
    }
}
