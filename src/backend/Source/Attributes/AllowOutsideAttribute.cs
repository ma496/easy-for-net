namespace Backend.Attributes;

/// <summary>
/// When applied to a class, struct, interface or enum within a feature, this attribute allows other
/// features to have a dependency on it. It is how a feature publishes a contract - a service
/// interface, the types that contract speaks in - while everything it does not carry stays private to
/// the slice.
/// </summary>
/// <remarks>
/// Structs are included because a contract may speak in a small value type - an identifier or a
/// target a question is asked about - and such a type is as much part of the published surface as the
/// interface that takes it.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
public sealed class AllowOutsideAttribute : Attribute
{
}
