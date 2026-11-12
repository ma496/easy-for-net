namespace Backend.Features.Tenancy.Endpoints.FeatureValues;

/// <summary>
/// This route group that prefixes the feature-management endpoints - reading and setting what a
/// tenant or an edition is entitled to - with the <c>features</c> segment. Endpoints in this area
/// declare only the remainder of their route.
/// </summary>
/// <remarks>
/// The folder is named for the rows it administers rather than for the word "features", which in this
/// codebase already means a vertical slice. The route keeps the shorter, friendlier segment.
/// </remarks>
sealed class FeatureValuesGroup : Group
{
    public FeatureValuesGroup()
    {
        Configure("features", ep => {});
    }
}
