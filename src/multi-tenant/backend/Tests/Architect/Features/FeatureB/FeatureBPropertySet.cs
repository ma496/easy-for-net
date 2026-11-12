using Backend.Tests.Architect.Features.FeatureA;

namespace Backend.Tests.Architect.Features.FeatureB;

// ReSharper disable NotAccessedField.Local
// ReSharper disable UnusedVariable
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.


/// <summary>
/// Test fixture class in FeatureB that has property setters that instantiate FeatureA types.
/// Used to verify that property setter body dependencies are correctly detected.
/// </summary>
public class FeatureBPropertySet
{
    private IFeatureAOneService _featureAOneService;
    private IFeatureAAllowOutsideService _featureAAllowOutsideService;

    /// <summary>
    /// Property setter that instantiates a forbidden FeatureA type in its body.
    /// </summary>
    public IFeatureAOneService FeatureAOneService
    {
        set
        {
            _featureAOneService = value;
            var x = new  FeatureAOneService();
        }
    }

    /// <summary>
    /// Property setter that instantiates an allowed FeatureA type in its body.
    /// </summary>
    public IFeatureAAllowOutsideService FeatureAAllowOutsideService
    {
        set
        {
            _featureAAllowOutsideService = value;
            var x = new  FeatureAAllowOutsideService();
        }
    }
}