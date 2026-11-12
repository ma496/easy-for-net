using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Extensions;

/// <summary>
/// Provides extension methods for object serialization.
/// </summary>
public static class JsonExtension
{
    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="options">Optional JsonSerializerOptions to customize serialization behavior.</param>
    /// <returns>A JSON string representation of the object.</returns>
    public static string ToJson<T>(this T obj, JsonSerializerOptions? options = null)
    {
        // If the object is null, return a JSON null literal.
        if (obj == null)
        {
            return "null";
        }

        // Default options if none are provided.
        // Using sensible defaults like ReferenceHandler.Preserve to handle object cycles.
        var defaultOptions = options ?? new JsonSerializerOptions
        {
            WriteIndented = true,                        // Makes the JSON output readable.
            ReferenceHandler = ReferenceHandler.Preserve // Important for handling circular references.
        };

        try
        {
            return JsonSerializer.Serialize(obj, defaultOptions);
        }
        catch (JsonException ex)
        {
            return $"{{ \"error\": \"Could not serialize object to JSON.\", \"details\": \"{ex.Message}\" }}";
        }
    }
}