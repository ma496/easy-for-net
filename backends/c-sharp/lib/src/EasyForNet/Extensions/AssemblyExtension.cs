using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EasyForNet.Extensions;

public static class AssemblyExtension
{
    public static List<Type> GetConcreteTypes(this Assembly assembly)
    {
        return assembly.GetTypes().Where(t => t.IsConcreteClass()).ToList();
    }
}