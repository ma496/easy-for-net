using Backend.Tests.Architect.Features.FeatureA;

namespace Backend.Tests.Architect.Features.FeatureB;

/// <summary>
/// Test fixture class in FeatureB that has methods returning FeatureA types.
/// Used to verify that return type dependencies are correctly detected.
/// </summary>
public class FeatureBMethodReturn
{
    /// <summary>
    /// Returns a forbidden FeatureA interface type.
    /// </summary>
    public IFeatureAOneService? MethodA()
    {
        return null;
    }
    
    /// <summary>
    /// Returns a generic list containing a forbidden FeatureA type.
    /// </summary>
    public List<FeatureAOneService>? MethodB()
    {
        return null;
    }
}