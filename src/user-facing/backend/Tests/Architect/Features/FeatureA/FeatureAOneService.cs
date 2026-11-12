// ReSharper disable UnusedVariable
namespace Backend.Tests.Architect.Features.FeatureA;

/// <summary>
/// Interface for a service in FeatureA without <see cref="AllowOutsideAttribute"/>,
/// so cross-feature usage from FeatureB should be detected as forbidden.
/// </summary>
public interface IFeatureAOneService
{
}

/// <summary>
/// Service implementation in FeatureA that directly instantiates a <see cref="NoDirectUseAttribute"/>-marked type.
/// Used to verify that NoDirectUse enforcement also applies within the same feature.
/// </summary>
public class FeatureAOneService : IFeatureAOneService
{
    public FeatureAOneService()
    {
        var featureANoDirectUseService = new FeatureANoDirectUseService();
    }
} 