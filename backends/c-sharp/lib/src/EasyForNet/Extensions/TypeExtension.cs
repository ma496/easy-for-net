using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ardalis.GuardClauses;

namespace EasyForNet.Extensions;

// TODO Do TypeExtension methods test
public static class TypeExtension
{
    public static bool IsPrimitiveOrString(this Type type)
    {
        return type.IsPrimitive || type == typeof(string);
    }

    public static bool IsConcreteClass(this Type type)
    {
        return type.IsClass && !type.IsAbstract;
    }

    public static List<FieldInfo> GetConstants(this Type type)
    {
        var fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                        BindingFlags.Static | BindingFlags.FlattenHierarchy);

        return fieldInfos.Where(fi => fi.IsLiteral && !fi.IsInitOnly).ToList();
    }

    public static bool IsDefaultConstructor(this Type t)
    {
        return t.IsValueType || t.GetConstructor(Type.EmptyTypes) != null;
    }

    public static bool HasProperty(this Type type, string propertyName)
    {
        Guard.Against.Null(type, nameof(type));
        Guard.Against.Null(propertyName, nameof(propertyName));

        var property = type.GetProperty(propertyName);
        return property != null;
    }

    public static bool IsEnumerableType(this Type type)
    {
        return typeof(IEnumerable).IsAssignableFrom(type);
    }

    public static bool IsNavigationProperty(this Type type)
    {
        return !type.IsValueType && type != typeof(string) && (type.IsClass || type.IsEnumerableType());
    }

    public static List<PropertyInfo> GetNavigationProperties(this Type type)
    {
        var properties = type.GetProperties()
            .Where(p => p.PropertyType.IsNavigationProperty())
            .ToList();
        return properties;
    }
}