namespace Backend.Features.Tenancy.Core.FeatureManagement;

/// <summary>
/// Implemented by a vertical slice to declare the features it owns. Each provider contributes one
/// logical group of related features to the central catalogue.
/// </summary>
/// <remarks>
/// This is the entitlement system, not the slice system. A slice's DI module is
/// <c>&lt;X&gt;Feature : IFeature</c>; its entitlement declarations are <c>&lt;X&gt;FeaturesProvider</c>,
/// beside the <c>&lt;X&gt;PermissionsProvider</c> it already has. The two never meet.
/// </remarks>
[AllowOutside]
public interface IFeatureDefinitionProvider
{
    /// <summary>
    /// Stable name of the group this provider contributes, used to look the group up.
    /// </summary>
    string GroupName { get; }

    /// <summary>
    /// What the management UI shows for the group. The group name unless stated.
    /// </summary>
    string GroupDisplayName => GroupName;

    /// <summary>
    /// Adds the provider's features to <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The builder context to populate.</param>
    void Define(FeatureDefinitionContext context);
}
