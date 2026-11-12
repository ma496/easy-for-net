using Backend.Tests.Architect.Features.FeatureA;

namespace Backend.Tests.Architect.Features.FeatureB;

// ReSharper disable UnusedParameter.Local


/// <summary>
/// Test fixture class in FeatureB that depends on FeatureA types through constructor parameters.
/// Used to verify that constructor parameter dependencies are correctly detected.
/// </summary>
public class FeatureBConstructor
{
    /// <summary>
    /// Accepts both forbidden (FeatureA) and allowed (AllowOutside) FeatureA types as constructor parameters.
    /// </summary>
    public FeatureBConstructor(IFeatureAOneService featureAOneService, FeatureAOneService? featureAOneService1,
                               IFeatureAAllowOutsideService featureAAllowOutsideService,
                               FeatureAAllowOutsideService? featureAAllowOutsideService1)
    {
        featureAAllowOutsideService.MethodOne(new());
    }
}