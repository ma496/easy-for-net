using Backend.Attributes;

namespace Backend.Tests.Architect.Features.FeatureA;

/// <summary>
/// Model class decorated with <see cref="AllowOutsideAttribute"/> to verify that
/// AllowOutside models are not flagged as forbidden cross-feature dependencies.
/// </summary>
[AllowOutside]
public class FeatureAOneAllowOutsideModel
{
    
}