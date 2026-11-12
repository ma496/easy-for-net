using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Features.Seo.Core;

/// <summary>
/// The shape checks the SEO page endpoints and <see cref="SeoSettings"/> share, and the template
/// matching resolution relies on.
/// </summary>
public static partial class SeoValidationRules
{
    /// <summary>
    /// A route without its locale prefix: <c>/</c>, or one or more <c>/segment</c>s with no trailing
    /// slash, where a segment is unreserved URL characters or a whole-segment placeholder <c>{name}</c>.
    /// </summary>
    [GeneratedRegex(@"^(/|(/([A-Za-z0-9\-._~%]+|\{[A-Za-z0-9_]+\}))+)$")]
    private static partial Regex PathPattern();

    /// <summary>Whether <paramref name="path"/> is a well-formed page path.</summary>
    public static bool IsValidPath(string? path) => path is not null && PathPattern().IsMatch(path);

    /// <summary>Whether <paramref name="path"/> is a template, standing for every path of its shape.</summary>
    public static bool IsTemplate(string path) => path.Contains('{');

    /// <summary>Whether <paramref name="value"/> is an absolute http or https URL.</summary>
    public static bool IsAbsoluteHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Whether <paramref name="value"/> parses as a JSON object or array.</summary>
    public static bool IsJsonObjectOrArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the concrete <paramref name="path"/> is matched by <paramref name="template"/>: the same
    /// number of segments, each placeholder segment matching any one segment and every other segment
    /// matching exactly.
    /// </summary>
    public static bool TemplateMatches(string template, string path)
    {
        var templateSegments = template.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (templateSegments.Length != pathSegments.Length)
        {
            return false;
        }

        for (var i = 0; i < templateSegments.Length; i++)
        {
            var segment = templateSegments[i];
            var isPlaceholder = segment.StartsWith('{') && segment.EndsWith('}');
            if (!isPlaceholder && !string.Equals(segment, pathSegments[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>How many placeholder segments a path holds - fewer is the more specific template.</summary>
    public static int PlaceholderCount(string path) => path.Count(character => character == '{');
}
