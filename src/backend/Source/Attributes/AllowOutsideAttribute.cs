namespace Backend.Attributes;

/// <summary>
/// When applied to a class, interface or enum within a feature, this attribute allows other features
/// to have a dependency on it. It is how a feature publishes a contract - a service interface, the
/// types that contract speaks in - while everything it does not carry stays private to the slice.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
public sealed class AllowOutsideAttribute : Attribute
{
}
