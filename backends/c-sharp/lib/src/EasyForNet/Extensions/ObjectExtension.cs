using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ardalis.GuardClauses;
using EasyForNet.Helpers;

namespace EasyForNet.Extensions;

// TODO Do ObjectExtension methods test
public static class ObjectExtension
{
    public static T Clone<T>(this T source)
    {
        // Don't serialize a null object, simply return the default for that object
        if (ReferenceEquals(source, null))
        {
            return default;
        }

        var serializerOptions = new JsonSerializerOptions 
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReferenceHandler = ReferenceHandler.Preserve,
        };

        var json = JsonHelper.ToJson(source, serializerOptions);

        return JsonHelper.Deserialize<T>(json, serializerOptions);
    }

    public static void SetPropertyValue(this object obj, string propertyName, object value)
    {
        Guard.Against.Null(obj, nameof(obj));
        Guard.Against.Null(propertyName, nameof(propertyName));

        var type = obj.GetType();
        Guard.Against.Null(type, nameof(type));

        if (!type.HasProperty(propertyName))
            throw new Exception($"{type.FullName} has no {propertyName} property.");

        type.GetProperty(propertyName)?.SetValue(obj, value);
    }

    public static object GetPropertyValue(this object obj, string propertyName)
    {
        Guard.Against.Null(obj, nameof(obj));
        Guard.Against.Null(propertyName, nameof(propertyName));

        var type = obj.GetType();
        Guard.Against.Null(type, nameof(type));

        if (!type.HasProperty(propertyName))
            throw new Exception($"{type.FullName} has no {propertyName} property.");

        return type.GetProperty(propertyName)?.GetValue(obj);
    }
}