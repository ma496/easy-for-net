using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace EasyForNet.Helpers;

[SuppressMessage("ReSharper", "StaticMemberInGenericType")]
public class EnumNext<T>
    where T : struct, Enum
{
    private readonly Dictionary<string, T> _lastEnumValue = new();

    public T Next()
    {
        var typeFullName = typeof(T).FullName ?? throw new Exception($"{typeof(T).FullName} can not be null");
        var enumValues = typeof(T).GetEnumValues().Cast<object>().ToList();

        if (!_lastEnumValue.ContainsKey(typeFullName))
        {
            _lastEnumValue.Add(typeFullName, (T) enumValues.First());
            return _lastEnumValue[typeFullName];
        }

        foreach (var enumValue in enumValues)
            if (Equals(enumValue, _lastEnumValue[typeFullName]))
            {
                var indexOf = enumValues.IndexOf(enumValue);
                if (indexOf < enumValues.Count - 1)
                {
                    _lastEnumValue[typeFullName] = (T) enumValues[indexOf + 1];
                    break;
                }

                _lastEnumValue[typeFullName] = (T) enumValues[0];
                break;
            }

        return _lastEnumValue[typeFullName];
    }

    public void Reset()
    {
        var typeFullName = typeof(T).FullName ?? throw new Exception($"{typeof(T).FullName} can not be null");
        if (_lastEnumValue.ContainsKey(typeFullName))
            _lastEnumValue.Remove(typeFullName);
    }
}