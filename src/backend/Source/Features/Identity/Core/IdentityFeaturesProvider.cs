namespace Backend.Features.Identity.Core;

/// <summary>
/// Declares the entitlement features for the Identity feature: whether a tenant may provision its own
/// accounts, and how many it may hold.
/// </summary>
/// <remarks>
/// This is the entitlement declaration, not the slice's DI module - that is
/// <see cref="Backend.Features.Identity.IdentityFeature"/>, and the two never meet. It sits beside
/// <see cref="IdentityPermissionsProvider"/> because the pair describes the same area from two sides:
/// what the plan includes, and what a role may be granted within it.
/// <para>
/// Both features ship enabled, which is what a template owes a project that has sold nothing yet.
/// </para>
/// </remarks>
public class IdentityFeaturesProvider : IFeatureDefinitionProvider
{
    public string GroupName => "Identity";

    public void Define(FeatureDefinitionContext context)
    {
        var userManagement = context.AddFeature(
            FeatureNames.Identity_UserManagement,
            "User management",
            BooleanValidator.TrueValue,
            description: "Whether the tenant may create and administer its own accounts.");

        // A child rather than a sibling: a seat count means nothing where accounts cannot be created
        // at all, and nesting it is what keeps the two from contradicting each other.
        userManagement.AddChild(
            FeatureNames.Identity_MaxUserCount,
            "Maximum accounts",
            "100",
            new FreeTextValueType(new NumericValidator(1, 1_000_000)),
            "How many accounts the tenant may hold.");
    }
}
