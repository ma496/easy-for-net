using Backend.Tests.Architect.Features.FeatureA;

namespace Backend.Tests.Architect.Features.FeatureB;

// ReSharper disable ArrangeAccessorOwnerBody


/// <summary>
/// Test fixture class in FeatureB that has property getters that instantiate FeatureA types.
/// Used to verify that property getter body dependencies are correctly detected.
/// </summary>
public class FeatureBPropertyGet
{
    /// <summary>
    /// Property getter that returns a new instance of a forbidden FeatureA type.
    /// </summary>
    public IFeatureAOneService FeatureAOneService
    {
        get
        {
            return new FeatureAOneService();
        }
    }

    /// <summary>
    /// Property getter that returns a new instance of an allowed FeatureA type.
    /// </summary>
    public IFeatureAAllowOutsideService FeatureAAllowOutsideService
    {
        get
        {
            return new FeatureAAllowOutsideService();
        }
    }
}