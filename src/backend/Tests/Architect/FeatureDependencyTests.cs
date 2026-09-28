namespace Backend.Tests.Architect;

using Backend.Tests.Architect.Features.FeatureA;
using Backend.Tests.Architect.Features.FeatureB;
using Backend.Tests.Architect.Features.FeatureB.Stat;

/// <summary>
/// Tests to verify that features do not have unauthorized dependencies on other features, and that the dependency detection logic works correctly.
/// </summary>
public class FeatureDependencyTests(App app) : AppTestsBase(app)
{
    /// <summary>
    /// Verifies that all production features (under Backend.Features) have no unwanted cross-feature dependencies.
    /// </summary>
    [Fact]
    public void Features_Should_Not_Have_Unwanted_Dependencies()
    {
      var assembly = typeof(global::Program).Assembly;
      const string baseFeatureNamespace = "Backend.Features";
      var featureDependencyTester = Service<IFeatureDependencyTester>();
      var testOutput = featureDependencyTester.Test(assembly, baseFeatureNamespace);
      
      Assert.True(testOutput.IsSuccess, "Feature dependency test failed:\n" + FormatFailureMessage(testOutput));
    }
    
    /// <summary>
    /// Verifies that test fixture features correctly detect forbidden and permitted cross-feature dependencies.
    /// </summary>
    [Fact]
    public void Features_Should_Have_Unwanted_Dependencies()
    {
        var assembly = GetType().Assembly;
        const string baseFeatureNamespace = "Backend.Tests.Architect.Features";
        var featureDependencyTester = Service<IFeatureDependencyTester>();
        var testOutput = featureDependencyTester.Test(assembly, baseFeatureNamespace);
      
        Assert.False(testOutput.IsSuccess);
        
        MustHaveForbiddenTypes(testOutput, typeof(FeatureBStat), 
            [typeof(IFeatureAOneService)]);
        MustHaveNotForbiddenTypes(testOutput, typeof(FeatureBStat), 
            [typeof(IFeatureAAllowOutsideService), typeof(FeatureAAllowOutsideService)]);
        MustHaveForbiddenTypes(testOutput, typeof(FeatureBConstructor), 
            [typeof(IFeatureAOneService), typeof(FeatureAOneService)]);
        MustHaveNotForbiddenTypes(testOutput, typeof(FeatureBConstructor), 
            [typeof(IFeatureAAllowOutsideService), typeof(FeatureAAllowOutsideService)]);
        MustHaveForbiddenTypes(testOutput, typeof(FeatureBConstructorBody), 
            [typeof(IFeatureAOneService), typeof(FeatureAOneService)]);
        MustHaveNotForbiddenTypes(testOutput, typeof(FeatureBConstructorBody), 
            [typeof(IFeatureAAllowOutsideService), typeof(FeatureAAllowOutsideService)]);
        MustHaveForbiddenTypes(testOutput, typeof(FeatureBField), 
            [typeof(IFeatureAOneService), typeof(FeatureAOneService)]);
        MustHaveNotForbiddenTypes(testOutput, typeof(FeatureBField), 
            [typeof(IFeatureAAllowOutsideService), typeof(FeatureAAllowOutsideService)]);
        MustHaveForbiddenTypes(testOutput, typeof(FeatureBProperty), 
            [typeof(IFeatureAOneService), typeof(FeatureAOneService)]);
        MustHaveNotForbiddenTypes(testOutput, typeof(FeatureBProperty), 
            [typeof(IFeatureAAllowOutsideService), typeof(FeatureAAllowOutsideService)]);
        MustHaveForbiddenTypes(testOutput, typeof(FeatureBPropertyGet), 
            [typeof(IFeatureAOneService), typeof(FeatureAOneService)]);
        MustHaveNotForbiddenTypes(testOutput, typeof(FeatureBPropertyGet), 
            [typeof(IFeatureAAllowOutsideService), typeof(FeatureAAllowOutsideService)]);
        MustHaveForbiddenTypes(testOutput, typeof(FeatureBPropertyGetExpression), 
            [typeof(IFeatureAOneService), typeof(FeatureAOneService)]);
        MustHaveNotForbiddenTypes(testOutput, typeof(FeatureBPropertyGetExpression), 
            [typeof(IFeatureAAllowOutsideService), typeof(FeatureAAllowOutsideService)]);
        MustHaveForbiddenTypes(testOutput, typeof(FeatureBPropertySet), 
            [typeof(IFeatureAOneService), typeof(FeatureAOneService)]);
        MustHaveNotForbiddenTypes(testOutput, typeof(FeatureBPropertySet), 
            [typeof(IFeatureAAllowOutsideService), typeof(FeatureAAllowOutsideService)]);
        MustHaveForbiddenTypes(testOutput, typeof(FeatureBMethodParameters), 
            [typeof(IFeatureAOneService), typeof(FeatureAOneService)]);
        MustHaveNotForbiddenTypes(testOutput, typeof(FeatureBMethodParameters), 
            [typeof(IFeatureAAllowOutsideService), typeof(FeatureAAllowOutsideService)]);
        MustHaveForbiddenTypes(testOutput, typeof(FeatureBMethodReturn), 
            [typeof(IFeatureAOneService), typeof(FeatureAOneService)]);
        MustHaveNotForbiddenTypes(testOutput, typeof(FeatureBMethodReturn), 
            [typeof(IFeatureAAllowOutsideService), typeof(FeatureAAllowOutsideService)]);
        MustHaveForbiddenTypes(testOutput, typeof(FeatureBMethodReturnTuple), 
            [typeof(IFeatureAOneService), typeof(FeatureAOneService)]);
        MustHaveNotForbiddenTypes(testOutput, typeof(FeatureBMethodReturnTuple), 
            [typeof(IFeatureAAllowOutsideService), typeof(FeatureAAllowOutsideService)]);
        MustHaveForbiddenTypes(testOutput, typeof(FeatureBMethodBody), 
            [typeof(IFeatureAOneService), typeof(FeatureAOneService)]);
        MustHaveNotForbiddenTypes(testOutput, typeof(FeatureBMethodBody), 
            [typeof(IFeatureAAllowOutsideService), typeof(FeatureAAllowOutsideService),
                typeof(FeatureAOneAllowOutsideModel), typeof(FeatureATwoAllowOutsideModel), typeof(FeatureAOneModel)]);
    }
  
    /// <summary>
    /// Formats a detailed failure message from the test output, listing each failed type and its forbidden dependencies.
    /// </summary>
    private static string FormatFailureMessage(FeatureDependencyTestOutput testOutput)
    {
        if (testOutput.IsSuccess)
        {
            return string.Empty;
        }
  
        var failedTypesMessages = testOutput.FailedTypes.Select(ft =>
        {
            if (!ft.ForbiddenTypes.Any())
            {
                return $"- Type '{ft.Type.FullName}' failed, but no specific forbidden dependencies were found. Check for base class or interface issues.";
            }
            
            var forbiddenTypeFullNames = ft.ForbiddenTypes.Select(ft1 => ft1.FullName).ToList();
  
            return $@"- Type '{ft.Type.FullName}' in feature '{testOutput.FeatureNamespace}' has forbidden dependencies on:
  - {string.Join("\n  - ", forbiddenTypeFullNames)}";
        });
  
        return string.Join("\n", failedTypesMessages);
    }

    /// <summary>
    /// Asserts that a specific type appears in the failed types list and has the specified forbidden dependencies.
    /// </summary>
    private static void MustHaveForbiddenTypes(FeatureDependencyTestOutput testOutput,
                                               Type type,
                                               Type[] forbiddenTypes)
    {
        var failedType = testOutput.FailedTypes.SingleOrDefault(ft => ft.Type.FullName == type.FullName);

        if (failedType == null)
            Assert.Fail($"No failed type '{type.FullName}' found.");

        foreach (var forbiddenType in forbiddenTypes)
        {
            if (failedType.ForbiddenTypes.All(x => x.FullName != forbiddenType.FullName))
                Assert.Fail($"No forbidden type '{forbiddenType.FullName}' found under '{type.FullName}'.");
        }
    }
    
    /// <summary>
    /// Asserts that a specific type appears in the failed types list but does NOT have the specified types as forbidden dependencies.
    /// </summary>
    private static void MustHaveNotForbiddenTypes(FeatureDependencyTestOutput testOutput,
                                               Type type,
                                               Type[] forbiddenTypes)
    {
        var failedType = testOutput.FailedTypes.SingleOrDefault(ft => ft.Type.FullName == type.FullName);

        if (failedType == null)
            Assert.Fail($"No failed type '{type.FullName}' found.");

        foreach (var forbiddenType in forbiddenTypes)
        {
            if (failedType.ForbiddenTypes.Any(x => x.FullName == forbiddenType.FullName))
                Assert.Fail($"This type '{forbiddenType.FullName}' should not forbidden under '{type.FullName}'.");
        }
    }
}
