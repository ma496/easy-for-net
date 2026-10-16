using Backend.Tests.Architect.Features.FeatureA;

namespace Backend.Tests.Architect.Features.FeatureB;

/// <summary>
/// Test fixture class in FeatureB that has a method returning a tuple containing FeatureA types.
/// Used to verify that tuple return type dependencies are correctly detected.
/// </summary>
public class FeatureBMethodReturnTuple
{
    /// <summary>
    /// Returns a tuple containing forbidden FeatureA types.
    /// </summary>
    public (IFeatureAOneService?, FeatureAOneService?) MethodA()
    {
        return (null, null);
    }
}