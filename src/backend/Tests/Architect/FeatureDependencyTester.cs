namespace Backend.Tests.Architect;

using System.Reflection;
using Backend.Attributes;
using Mono.Cecil;
using NetArchTest.Rules;
using TypeDefinition = Mono.Cecil.TypeDefinition;

/// <summary>
/// Output of a feature dependency test containing success status, the failing feature namespace, and details of failed types.
/// </summary>
public record FeatureDependencyTestOutput(bool IsSuccess,
                                          string FeatureNamespace,
                                          IReadOnlyList<FailedTypeInfo> FailedTypes);

/// <summary>
/// Information about a type that failed the dependency check, including the forbidden dependencies it references.
/// </summary>
public record FailedTypeInfo(Type Type, IReadOnlyList<TypeDefinition> ForbiddenTypes);

/// <summary>
/// Service for testing feature dependency rules across an assembly.
/// </summary>
public interface IFeatureDependencyTester
{
    /// <summary>
    /// Tests all features within the given assembly namespace for unauthorized cross-feature dependencies.
    /// </summary>
    FeatureDependencyTestOutput Test(Assembly assembly, string baseFeatureNamespace);
}

/// <summary>
/// Implements feature dependency testing by discovering features and applying <see cref="FeatureDependencyRule"/> to each.
/// </summary>
public class FeatureDependencyTester : IFeatureDependencyTester {
    /// <summary>
    /// Tests all features within the given assembly namespace for unauthorized cross-feature dependencies.
    /// </summary>
    public FeatureDependencyTestOutput Test(Assembly assembly, string baseFeatureNamespace)
    {
        var featureNamespaces = Types.InAssembly(assembly)
                                     .That().ResideInNamespaceStartingWith(baseFeatureNamespace)
                                     .GetTypes()
                                     .Select(t => GetFeatureFromNamespace(baseFeatureNamespace, t.Namespace ?? ""))
                                     .Where(n => !string.IsNullOrEmpty(n))
                                     .Distinct()
                                     .ToList();

        var isSuccessFlag = true;
        var featureNamespaceFlag = string.Empty;
        var failedTypesFlag = new List<FailedTypeInfo>();

        foreach (var featureNamespace in featureNamespaces)
        {
            var otherFeatureNamespaces = featureNamespaces.Where(n => n != featureNamespace).ToList();
            if (!otherFeatureNamespaces.Any())
            {
                continue;
            }

            var rule = new FeatureDependencyRule(otherFeatureNamespaces);

            var result = Types.InAssembly(assembly)
                              .That()
                              .ResideInNamespaceStartingWith(featureNamespace)
                              .Should()
                              .MeetCustomRule(rule)
                              .GetResult();

            if (result.IsSuccessful)
            {
                continue;
            }
            
            var assemblyDefinition = AssemblyDefinition.ReadAssembly(assembly.Location);

            var failedTypes = result.FailingTypes.Select<Type, FailedTypeInfo>(t =>
            {
                // Reflection spells a nested type Outer+Nested where Cecil expects Outer/Nested, so a
                // failing nested type would otherwise be reported with no dependencies at all.
                var typeDefinition = assemblyDefinition.MainModule.GetType(t.FullName?.Replace('+', '/') ?? string.Empty);
                if (typeDefinition == null)
                {
                    return new(t, []);
                }

                var dependencies = ArchitectHelper.GetDependencies(typeDefinition);
                var forbidden = dependencies
                    .Where(d => otherFeatureNamespaces.Any(ns => d.FullName.StartsWith(ns)) &&
                                (!d.HasCustomAttributes ||
                                 d.CustomAttributes.All(a => a.AttributeType.FullName != typeof(AllowOutsideAttribute).FullName)))
                    .ToList();

                return new(t, forbidden);
            }).ToList();

            isSuccessFlag = false;
            featureNamespaceFlag = featureNamespace;
            failedTypesFlag = failedTypes;
            
            break;
        }
        
        return new(isSuccessFlag, featureNamespaceFlag, failedTypesFlag);
    }
    
    /// <summary>
    /// Extracts the top-level feature namespace from a fully qualified namespace relative to the base feature namespace.
    /// </summary>
    private static string GetFeatureFromNamespace(string baseFeatureNamespace, string @namespace)
    {
        if (string.IsNullOrEmpty(@namespace) || !@namespace.StartsWith($"{baseFeatureNamespace}."))
        {
            return string.Empty;
        }

        var parts = @namespace.Substring($"{baseFeatureNamespace}.".Length).Split('.');
        return $"{baseFeatureNamespace}.{parts.First()}";
    }
}