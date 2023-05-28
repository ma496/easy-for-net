using System.Collections.Generic;
using JetBrains.Annotations;

namespace EasyForNet.Extensions;

public static class EnumerableExtension
{
    [ContractAnnotation("source:null => true")]
    public static bool IsNull<T>(this IEnumerable<T> source)
    {
        return source == null;
    }
}