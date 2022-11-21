using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;

namespace EasyForNet.EntityFramework.Helpers;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class UniqueProperty
{
    public UniqueProperty(string name, bool isAllowDefaultValue = false)
    {
        Guard.Against.Null(name, nameof(name));

        Name = name;
        IsAllowDefaultValue = isAllowDefaultValue;
    }

    public string Name { get; }
    public bool IsAllowDefaultValue { get; }
}