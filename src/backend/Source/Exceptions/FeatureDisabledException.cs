namespace Backend.Exceptions;

/// <summary>
/// Exception thrown when an operation is reached whose feature the acting tenant's plan does not
/// include.
/// </summary>
/// <remarks>
/// This is entitlement, not authorization. The refusal is the same for every caller in the tenant,
/// its administrator included, and it means the plan does not cover this rather than that the caller
/// lacks a grant - so it carries its own error code and its own response, rather than being reported
/// as a permission failure. <c>ExceptionProcessor</c> turns it into a 403.
/// </remarks>
/// <param name="featureName">The feature that is not enabled.</param>
public sealed class FeatureDisabledException(string featureName)
    : Exception($"The feature '{featureName}' is not enabled.")
{
    /// <summary>
    /// The feature that is not enabled.
    /// </summary>
    public string FeatureName { get; } = featureName;
}
