namespace Backend.Attributes;

/// <summary>
/// Attribute to indicate that a class should not be used directly.
/// It should be consumed via one of its implemented interfaces.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NoDirectUseAttribute : Attribute
{
}