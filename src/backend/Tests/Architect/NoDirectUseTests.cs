namespace Backend.Tests.Architect;

using Backend.Attributes;
using Mono.Cecil;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

/// <summary>
/// Tests to verify that types decorated with <see cref="NoDirectUseAttribute"/> are not directly referenced by other types,
/// ensuring they are only accessed through their interfaces.
/// </summary>
public class NoDirectUseTests()
{
    /// <summary>
    /// Verifies that no type in the production assembly (except those with <see cref="BypassNoDirectUseAttribute"/>)
    /// has a direct dependency on any type marked with <see cref="NoDirectUseAttribute"/>.
    /// </summary>
    [Fact]
    public void ClassesWithNoDirectUseAttribute_ShouldNotBeUsedDirectly()
    {
        // Arrange
        var assembly = typeof(global::Program).Assembly;

        var typesWithNoDirectUseAttribute = Types.InAssembly(assembly)
            .That()
            .HaveCustomAttribute(typeof(NoDirectUseAttribute))
            .GetTypes()
            .ToList();

        var typesWithNoDirectUseAttributeFullNames = typesWithNoDirectUseAttribute
                                                     .Select(x => x.FullName)
                                                     .ToArray();

        if (typesWithNoDirectUseAttribute.Count == 0)
        {
            // No types to test, so the test passes by default.
            return;
        }

        // Act
        var testResult = Types.InAssembly(assembly)
            .That()
            .DoNotHaveCustomAttribute(typeof(NoDirectUseAttribute))
            .And()
            .DoNotHaveCustomAttribute(typeof(BypassNoDirectUseAttribute))
            .And()
            .DoNotHaveName("Program")
            .ShouldNot()
            .HaveDependencyOnAny(typesWithNoDirectUseAttributeFullNames)
            .GetResult();

        // Assert
        Assert.True(testResult.IsSuccessful, GetFailingTypesMessage(testResult, typesWithNoDirectUseAttribute));
    }
    
    /// <summary>
    /// Verifies that types in the test assembly correctly reference types with <see cref="NoDirectUseAttribute"/> directly,
    /// confirming that the detection logic in <see cref="NoDirectUseAttribute"/> enforcement works as expected.
    /// </summary>
    [Fact]
    public void ClassesWithNoDirectUseAttribute_ShouldBeUsedDirectly()
    {
        // Arrange
        var assembly = GetType().Assembly;

        var typesWithNoDirectUseAttribute = Types.InAssembly(assembly)
                                                 .That()
                                                 .HaveCustomAttribute(typeof(NoDirectUseAttribute))
                                                 .GetTypes()
                                                 .ToList();

        var typesWithNoDirectUseAttributeFullNames = typesWithNoDirectUseAttribute
                                                     .Select(x => x.FullName)
                                                     .ToArray();

        if (typesWithNoDirectUseAttribute.Count == 0)
        {
            // No types to test, so the test passes by default.
            return;
        }

        // Act
        var testResult = Types.InAssembly(assembly)
                              .That()
                              .DoNotHaveCustomAttribute(typeof(NoDirectUseAttribute))
                              .And()
                              .DoNotHaveCustomAttribute(typeof(BypassNoDirectUseAttribute))
                              .And()
                              .DoNotHaveName("Program")
                              .ShouldNot()
                              .HaveDependencyOnAny(typesWithNoDirectUseAttributeFullNames)
                              .GetResult();

        // Assert
        Assert.False(testResult.IsSuccessful);
    }

    /// <summary>
    /// Formats a detailed failure message listing types that violate the NoDirectUse rule along with their forbidden dependencies.
    /// </summary>
    private static string GetFailingTypesMessage(TestResult result, IReadOnlyList<Type> typesWithNoDirectUseAttribute)
    {
        if (result.IsSuccessful || result.FailingTypes == null)
        {
            return string.Empty;
        }

        var failingTypesDetails = result.FailingTypes.Select(failingType =>
        {
            var module = ModuleDefinition.ReadModule(failingType.Module.FullyQualifiedName);
            // Reflection spells a nested type Outer+Nested where Cecil expects Outer/Nested, so a
            // failing nested type would otherwise be reported as unanalyzable.
            var cecilType = module.GetType(failingType.FullName?.Replace('+', '/') ?? string.Empty);

            if (cecilType == null) return $"{failingType.FullName} (could not analyze dependencies)";

            var dependencies = ArchitectHelper.GetDependencies(cecilType);
            var forbiddenDependencies = dependencies
                .Where(dep => typesWithNoDirectUseAttribute.Any(t => t.FullName == dep.FullName))
                .Select(d => d.FullName)
                .ToList();

            if (forbiddenDependencies.Count != 0)
            {
                return $"{failingType.FullName} -> [{string.Join(", ", forbiddenDependencies)}]";
            }

            return $"{failingType.FullName} (unexpected dependency)";
        });

        return $"The following types violate the NoDirectUse rule (Type -> Forbidden Dependencies):\n- {string.Join("\n- ", failingTypesDetails)}";
    }
}