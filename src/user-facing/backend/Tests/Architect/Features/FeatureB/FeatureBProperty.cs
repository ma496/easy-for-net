using Backend.Tests.Architect.Features.FeatureA;

namespace Backend.Tests.Architect.Features.FeatureB;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.


/// <summary>
/// Test fixture class in FeatureB that has properties of FeatureA types.
/// Used to verify that property type dependencies are correctly detected.
/// </summary>
public class FeatureBProperty
{
    public IFeatureAOneService FeatureAOneService { get; set; }
    public FeatureAOneService FeatureAOneService1 { get; set; }
    public IFeatureAAllowOutsideService FeatureAAllowOutsideService { get; set; }
    public FeatureAAllowOutsideService? FeatureAAllowOutsideService1 { get; set; }
}