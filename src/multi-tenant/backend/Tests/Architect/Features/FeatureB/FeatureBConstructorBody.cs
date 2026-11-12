using Backend.Tests.Architect.Features.FeatureA;

namespace Backend.Tests.Architect.Features.FeatureB;

// ReSharper disable UnusedVariable


/// <summary>
/// Test fixture class in FeatureB that depends on FeatureA types within the constructor body.
/// Used to verify that method body dependency resolution detects local variable instantiations.
/// </summary>
public class FeatureBConstructorBody
{
    /// <summary>
    /// Instantiates both forbidden and allowed FeatureA types as local variables in the constructor body.
    /// </summary>
    public FeatureBConstructorBody()
    {
        IFeatureAOneService featureAOneService = new FeatureAOneService();
        var featureAOneService1 = new FeatureAOneService();
        IFeatureAAllowOutsideService featureAAllowOutsideService = new FeatureAAllowOutsideService();
        var featureAAllowOutsideService1 = new FeatureAAllowOutsideService();
    }
}