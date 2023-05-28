using System.Collections;
using JetBrains.Annotations;

namespace EasyForNet.Extensions;

public static class CollectionExtension
{
    [ContractAnnotation("source:null => true")]
    public static bool IsNull(this ICollection source)
    {
        return source == null;
    }
    
    [ContractAnnotation("source:null => true")]
    public static bool IsNullOrEmpty(this ICollection source)
    {
        return source == null || source.Count == 0;
    }
}