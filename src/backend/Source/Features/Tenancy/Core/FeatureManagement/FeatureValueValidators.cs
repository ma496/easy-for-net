namespace Backend.Features.Tenancy.Core.FeatureManagement;

using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Validates the textual form of a feature value before it is stored.
/// </summary>
/// <remarks>
/// A feature value is always persisted as text, whatever it means, so the validator is the only thing
/// that knows a value is a flag, a number or one of a fixed set. <see cref="Properties"/> travels to
/// the management UI so the editor can constrain the input the same way the API will, rather than
/// letting an administrator discover the rule by being refused.
/// </remarks>
[AllowOutside]
public interface IFeatureValueValidator
{
    /// <summary>
    /// Stable name of the validator, which the management UI uses to choose an input control.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The validator's parameters - bounds, lengths, patterns - rendered as display-ready strings.
    /// </summary>
    IReadOnlyDictionary<string, string> Properties { get; }

    /// <summary>
    /// Whether <paramref name="value"/> is acceptable for the feature this validator guards.
    /// </summary>
    /// <param name="value">The candidate value, or <see langword="null"/> to clear the override.</param>
    /// <returns><see langword="true"/> when the value may be stored.</returns>
    bool IsValid(string? value);
}

/// <summary>
/// Accepts anything. The validator a free-text feature uses when it states no constraint of its own.
/// </summary>
[AllowOutside]
public sealed class AlwaysValidValidator : IFeatureValueValidator
{
    public string Name => "AlwaysValid";
    public IReadOnlyDictionary<string, string> Properties => new Dictionary<string, string>();

    /// <inheritdoc/>
    public bool IsValid(string? value) => true;
}

/// <summary>
/// Accepts only the two spellings a toggle stores, so a toggle can never hold a value nothing can read.
/// </summary>
/// <remarks>
/// Comparison is case-insensitive on read but <see cref="TrueValue"/> and <see cref="FalseValue"/> are
/// what the system writes, so stored values stay uniform however they were typed.
/// </remarks>
[AllowOutside]
public sealed class BooleanValidator : IFeatureValueValidator
{
    public const string TrueValue = "true";
    public const string FalseValue = "false";

    public string Name => "Boolean";
    public IReadOnlyDictionary<string, string> Properties => new Dictionary<string, string>();

    /// <inheritdoc/>
    public bool IsValid(string? value)
        => value is null
           || string.Equals(value, TrueValue, StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, FalseValue, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Accepts a whole number within an inclusive range. The validator behind a numeric limit such as a
/// seat count or an upload ceiling.
/// </summary>
/// <param name="minimum">Smallest accepted value.</param>
/// <param name="maximum">Largest accepted value.</param>
[AllowOutside]
public sealed class NumericValidator(long minimum = long.MinValue, long maximum = long.MaxValue) : IFeatureValueValidator
{
    public string Name => "Numeric";

    public IReadOnlyDictionary<string, string> Properties => new Dictionary<string, string>
    {
        ["minimum"] = minimum.ToString(CultureInfo.InvariantCulture),
        ["maximum"] = maximum.ToString(CultureInfo.InvariantCulture)
    };

    /// <inheritdoc/>
    public bool IsValid(string? value)
    {
        if (value is null)
        {
            return true;
        }
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
               && parsed >= minimum
               && parsed <= maximum;
    }
}

/// <summary>
/// Accepts text up to a length, optionally matching a pattern.
/// </summary>
/// <param name="maximumLength">Longest accepted value.</param>
/// <param name="pattern">Regular expression the whole value must match, or <see langword="null"/> for none.</param>
[AllowOutside]
public sealed class StringLengthValidator(int maximumLength, string? pattern = null) : IFeatureValueValidator
{
    private readonly Regex? _regex = pattern is null ? null : new Regex(pattern, RegexOptions.CultureInvariant);

    public string Name => "StringLength";

    public IReadOnlyDictionary<string, string> Properties
    {
        get
        {
            var properties = new Dictionary<string, string>
            {
                ["maximumLength"] = maximumLength.ToString(CultureInfo.InvariantCulture)
            };
            if (pattern is not null)
            {
                properties["pattern"] = pattern;
            }
            return properties;
        }
    }

    /// <inheritdoc/>
    public bool IsValid(string? value)
    {
        if (value is null)
        {
            return true;
        }
        return value.Length <= maximumLength && (_regex is null || _regex.IsMatch(value));
    }
}

/// <summary>
/// Accepts only one of a fixed set of values, so a selection feature cannot drift to a value the
/// application has no branch for.
/// </summary>
/// <param name="items">The values on offer.</param>
[AllowOutside]
public sealed class SelectionValidator(IReadOnlyList<SelectionItem> items) : IFeatureValueValidator
{
    public string Name => "Selection";

    public IReadOnlyDictionary<string, string> Properties => new Dictionary<string, string>
    {
        ["items"] = string.Join(',', items.Select(item => item.Value))
    };

    /// <inheritdoc/>
    public bool IsValid(string? value)
        => value is null || items.Any(item => string.Equals(item.Value, value, StringComparison.Ordinal));
}
