using Backend.Tests.Architect.Features.FeatureA;

namespace Backend.Tests.Architect.Features.FeatureB;

#pragma warning disable CS0169 // Field is never used
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.


/// <summary>
/// Test fixture class in FeatureB that declares fields of FeatureA types.
/// Used to verify that field-level dependencies are correctly detected.
/// </summary>
public class FeatureBField
{
    private IFeatureAOneService _featureAOneService;
    private FeatureAOneService? _featureAOneService1;
    private IFeatureAAllowOutsideService _featureAAllowOutsideService;
    private FeatureAAllowOutsideService? _featureAAllowOutsideService1;
}