namespace Backend.Attributes;

/// <summary>
/// Attribute to indicate that a class should bypass no direct use tests.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public class BypassNoDirectUseAttribute : Attribute
{
}