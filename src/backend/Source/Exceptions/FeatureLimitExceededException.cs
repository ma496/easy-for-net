namespace Backend.Exceptions;

/// <summary>
/// Exception thrown when an operation would take the acting tenant beyond a numeric limit its plan
/// sets - one account too many, one upload too large.
/// </summary>
/// <remarks>
/// The entitlement counterpart of <see cref="FeatureDisabledException"/> for a feature that holds a
/// number rather than a yes or no. The refusal is the same for every caller in the tenant, its
/// administrator included, so it carries its own error code rather than being reported as a
/// permission failure or a validation failure. <c>ExceptionProcessor</c> turns it into a 403.
/// </remarks>
/// <param name="featureName">The feature whose limit would be exceeded.</param>
/// <param name="limit">The limit in force, in the feature's own unit.</param>
public sealed class FeatureLimitExceededException(string featureName, long limit)
    : Exception($"The limit of {limit} set by the feature '{featureName}' has been reached.")
{
    /// <summary>
    /// The feature whose limit would be exceeded.
    /// </summary>
    public string FeatureName { get; } = featureName;

    /// <summary>
    /// The limit in force, in the feature's own unit.
    /// </summary>
    public long Limit { get; } = limit;
}
