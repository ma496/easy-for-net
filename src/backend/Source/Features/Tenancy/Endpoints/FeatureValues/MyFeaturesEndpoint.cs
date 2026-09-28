namespace Backend.Features.Tenancy.Endpoints.FeatureValues;

using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>GET /features/mine</c> to tell the caller what their own plan
/// includes.
/// </summary>
/// <remarks>
/// It declares no permission on purpose. Administering entitlements is a platform act - a tenant that
/// could write its own would simply switch on whatever its plan withholds - but asking what your own
/// plan gives you is not administration, and the web app needs the answer to decide which parts of the
/// interface are worth showing. Only features marked visible to clients are reported, so a feature
/// that describes something the browser has no business knowing stays on the server.
/// </remarks>
sealed class MyFeaturesEndpoint(IFeatureDefinitionService featureDefinitionService,
                                IFeatureValueResolver featureValueResolver,
                                ITenantContext tenantContext)
    : EndpointWithoutRequest<MyFeaturesResponse>
{
    public override void Configure()
    {
        Get("mine");
        Group<FeatureValuesGroup>();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        // A caller acting in no tenant is inside no plan, so the answer is what the deployment and the
        // declarations say - which is also what the platform tier itself operates under.
        var target = new FeatureTarget(tenantContext.IsResolved ? tenantContext.CurrentTenantId : null);
        var effective = await featureValueResolver.ResolveAsync(target, cancellationToken);

        var features = new Dictionary<string, string?>(StringComparer.Ordinal);
        var enabled = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var feature in featureDefinitionService.GetAll().Where(feature => feature.IsVisibleToClients))
        {
            features[feature.Name] = effective.GetOrNull(feature.Name);
            enabled[feature.Name] = effective.IsEnabled(feature.Name);
        }

        await Send.ResponseAsync(new MyFeaturesResponse
        {
            Features = features,
            Enabled = enabled
        }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Response payload describing what the caller's own plan includes.
/// </summary>
public sealed class MyFeaturesResponse
{
    /// <summary>
    /// Every client-visible feature's effective value, by name.
    /// </summary>
    public Dictionary<string, string?> Features { get; set; } = [];

    /// <summary>
    /// Whether each of those features is actually in force - which for a child feature also depends on
    /// the toggles above it, so the web app does not have to work the hierarchy out for itself.
    /// </summary>
    public Dictionary<string, bool> Enabled { get; set; } = [];
}
