using System;

namespace EasyForNet.Modules;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DependOnAttribute : Attribute
{
    public DependOnAttribute(Type moduleType)
    {
        ModuleType = moduleType;
    }

    public Type ModuleType { get; }
}