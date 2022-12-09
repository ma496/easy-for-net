using JetBrains.Annotations;

namespace System;

public static class StringExtensions
{
    [ContractAnnotation("str:null => true")]
    public static bool IsNullOrEmpty(this string str)
    {
        return string.IsNullOrEmpty(str);
    }

    [ContractAnnotation("str:null => true")]
    public static bool IsNullOrWhiteSpace(this string str)
    {
        return string.IsNullOrWhiteSpace(str);
    }
}
