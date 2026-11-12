using System.Diagnostics.CodeAnalysis;
using Backend.Tests.Architect.Features.FeatureA;
using Mono.Cecil;
using TypeDefinition = Mono.Cecil.TypeDefinition;

namespace Backend.Tests.Architect;

/// <summary>
/// Tests for <see cref="ArchitectHelper.GetDependencies"/> to verify the dependency resolution logic.
/// </summary>
public class ArchitectHelperTests(App app) : AppTestsBase(app) 
{
    /// <summary>
    /// Verifies that <see cref="ArchitectHelper.GetDependencies"/> correctly resolves all dependencies including interfaces, base classes, fields, method parameters, and local variables.
    /// </summary>
    [Fact]
    public void GetDependencies()
    {
        var assembly = GetType().Assembly;
        var assemblyDefinition = AssemblyDefinition.ReadAssembly(assembly.Location);
        var type = typeof(ServiceOne);
        var cecilTypeName = type.FullName?.Replace('+', '/') ?? string.Empty;
        var typeDefinition = assemblyDefinition.MainModule.GetType(cecilTypeName);
        
        Assert.NotNull(typeDefinition);
        
        var dependencies = ArchitectHelper.GetDependencies(typeDefinition);
        
        Assert.NotNull(dependencies);
        MustHaveDependencies(dependencies, 
            typeof(IFeatureAOneService), typeof(FeatureAOneService),
            typeof(int), typeof(string), typeof(double), typeof(float)
            );
    }

    /// <summary>
    /// Asserts that all specified types are present in the resolved dependencies list.
    /// </summary>
    private static void MustHaveDependencies(IReadOnlyList<TypeDefinition> dependencies, params Type[] mustHaveTypes)
    {
        var dependenciesFullName = dependencies.Select(x => x.FullName).ToList();
        foreach (var type in mustHaveTypes)
        {
            if (dependencies.All(x => x.FullName != type.FullName))
                Assert.Fail($"- Type '{type.FullName}' not found in dependencies\n{string.Join('\n', dependenciesFullName)}.");
        }
    }


#pragma warning disable CS9113
    /// <summary>
    /// Abstract base class used as a test fixture for dependency resolution, containing fields, constants, and static methods.
    /// </summary>
    public abstract class ServiceOneBase(FeatureAOneService featureAOneService)
    {
        const string Name = "ServiceOne";

        /// <summary>
        /// Multiplies the input by 3. Used to test static method dependencies.
        /// </summary>
        protected static int MultiplyBy3(int number)
            => number * 3;

        /// <summary>
        /// Private method with local variables. Used to test local variable dependency resolution.
        /// </summary>
        private static void MethodOne()
        {
        #pragma warning disable CS0219 // Variable is assigned but its value is never used
            var x = 33.4;
            var y = 53.43f;
        #pragma warning restore CS0219 // Variable is assigned but its value is never used
        }
    }
    #pragma warning restore CS9113
    
    #pragma warning disable CS9113
    /// <summary>
    /// Concrete test class inheriting from <see cref="ServiceOneBase"/>, used to verify that interface dependencies through constructor are resolved.
    /// </summary>
    public class ServiceOne(IFeatureAOneService _) : ServiceOneBase(new())
    {
    }
    #pragma warning restore CS9113
}