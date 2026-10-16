using System.Diagnostics.CodeAnalysis;
using Backend.Tests.Architect.Features.FeatureA;

namespace Backend.Tests.Architect.Features.FeatureB;

[SuppressMessage("Performance", "CA1822:Mark members as static")]
/// <summary>
/// Test fixture class in FeatureB that has expression-bodied property getters referencing FeatureA types.
/// Used to verify that expression-bodied property dependencies are correctly detected.
/// </summary>
public class FeatureBPropertyGetExpression
{
    /// <summary>
    /// Expression-bodied property getter that returns a new instance of a forbidden FeatureA type.
    /// </summary>
    public IFeatureAOneService FeatureAOneService => new FeatureAOneService();
    /// <summary>
    /// Expression-bodied property getter that returns a new instance of an allowed FeatureA type.
    /// </summary>
    public IFeatureAAllowOutsideService FeatureAAllowOutsideService => new FeatureAAllowOutsideService();
}