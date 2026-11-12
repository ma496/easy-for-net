namespace Backend.Tests.Architect.Features.FeatureA;

/// <summary>
/// Simple model class in FeatureA used to test cross-feature dependency detection.
/// This class has no <see cref="AllowOutsideAttribute"/>, so references from FeatureB should be forbidden.
/// </summary>
public class FeatureAOneModel
{
    
}