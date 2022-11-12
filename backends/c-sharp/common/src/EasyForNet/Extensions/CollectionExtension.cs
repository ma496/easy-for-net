using System.Collections;
using JetBrains.Annotations;

namespace EasyForNet.Extensions
{
    public static class CollectionExtension
    {
        [ContractAnnotation("source:null => true")]
        public static bool IsNullOrEmpty(this ICollection source)
        {
            return source == null || source.Count <= 0;
        }
    }
}