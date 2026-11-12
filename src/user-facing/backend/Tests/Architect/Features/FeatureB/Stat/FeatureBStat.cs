using Backend.Tests.Architect.Features.FeatureA;

namespace Backend.Tests.Architect.Features.FeatureB.Stat;

/// <summary>
/// Static class in FeatureB that references a FeatureA type through a method parameter.
/// Used to verify that static class dependencies are correctly detected.
/// </summary>
public static class FeatureBStat
{
    /// <summary>
    /// Static method accepting a forbidden FeatureA interface type as a parameter.
    /// </summary>
    public static void Sale(IFeatureAOneService  featureAOneService)
    {
        
    }
}