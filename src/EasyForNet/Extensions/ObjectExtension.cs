using System;
using Ardalis.GuardClauses;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace EasyForNet.Extensions
{
    public static class ObjectExtension
    {
        public static string ToJson(this object obj)
        {
            var json = JsonConvert.SerializeObject(obj, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });
            return json;
        }
        
        public static T Clone<T>(this T source)
        {
            // Don't serialize a null object, simply return the default for that object
            if (ReferenceEquals(source, null))
            {
                return default;
            }

            var serializeSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
            var json = JsonConvert.SerializeObject(source, serializeSettings);
            
            var deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            return JsonConvert.DeserializeObject<T>(json, deserializeSettings);
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
}