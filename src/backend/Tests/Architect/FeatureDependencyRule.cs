namespace Backend.Tests.Architect;

using Backend.Attributes;
using NetArchTest.Rules;

/// <summary>
/// Custom NetArchTest rule that checks if a type has forbidden dependencies on types from other features.
/// Types with <see cref="AllowOutsideAttribute"/> are permitted as cross-feature dependencies.
/// </summary>
public class FeatureDependencyRule(IEnumerable<string> otherFeatureNamespaces) : ICustomRule
{
    /// <summary>
    /// Determines whether the given type meets the feature dependency rule by checking for unauthorized cross-feature references.
    /// </summary>
    public bool MeetsRule(Mono.Cecil.TypeDefinition type)
    {
        if (type.IsInterface || type is { IsAbstract: true, IsSealed: false })
        {
            return true;
        }

        var dependencies = ArchitectHelper.GetDependencies(type);

        foreach (var dependency in dependencies)
        {
            if (otherFeatureNamespaces.Any(ns => dependency.FullName.StartsWith(ns)))
            {
                if (!dependency.HasCustomAttributes || dependency.CustomAttributes.All(a => a.AttributeType.FullName != typeof(AllowOutsideAttribute).FullName))
                {
                    return false; // Found a forbidden dependency
                }
            }
        }

        return true; // No forbidden dependencies found
    }
}
