using Backend.Tests.Architect.Features.FeatureA;

namespace Backend.Tests.Architect.Features.FeatureB;

/// <summary>
/// Test fixture class in FeatureB that has method parameters referencing FeatureA types.
/// Used to verify that method parameter dependencies are correctly detected.
/// </summary>
public class FeatureBMethodParameters : FeatureBMethodParametersBaseClass
{
    /// <summary>
    /// Method with a parameter of a forbidden FeatureA interface type.
    /// </summary>
    public void MethodOne(IFeatureAOneService featureAOneService) { }
}

/// <summary>
/// Base class used to test that generic method parameters (e.g., Action&lt;FeatureAOneService&gt;) are also detected as dependencies.
/// </summary>
public class FeatureBMethodParametersBaseClass
{
    /// <summary>
    /// Method with a generic parameter that references a forbidden FeatureA type.
    /// </summary>
    public void BaseMethodOne(Action<FeatureAOneService> action)
    {}
}