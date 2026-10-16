using Backend.Tests.Architect.Features.FeatureA;

namespace Backend.Tests.Architect.Features.FeatureB;

// ReSharper disable UnusedVariable


/// <summary>
/// Test fixture class in FeatureB that references FeatureA types within a method body.
/// Used to verify that method body instructions (local variables, method calls) are inspected for dependencies.
/// </summary>
public class FeatureBMethodBody
{
    /// <summary>
    /// Instantiates and uses both forbidden and allowed FeatureA types inside the method body.
    /// </summary>
    public void MethodA()
    {
        IFeatureAOneService featureAOneService = new FeatureAOneService();
        IFeatureAAllowOutsideService featureAAllowOutsideService = new FeatureAAllowOutsideService();
        featureAAllowOutsideService.MethodOne(new());
        var x = featureAAllowOutsideService.MethodTwo();
        featureAAllowOutsideService.MethodThree();
    }
}