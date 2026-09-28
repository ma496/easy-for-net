namespace Backend.Exceptions;

/// <summary>
/// Exception thrown when code asks about a feature that no
/// <c>IFeatureDefinitionProvider</c> declares. The catalogue is code, so an undeclared name is a
/// programming error - a typo or a feature removed without its callers - rather than anything a
/// request can cause, and failing loudly is what keeps a misspelt name from quietly resolving to its
/// default and hiding a permission forever.
/// </summary>
/// <param name="featureName">The feature name that was asked about.</param>
public sealed class FeatureNotDefinedException(string featureName)
    : Exception($"No feature named '{featureName}' has been defined.")
{
    /// <summary>
    /// The feature name that was asked about.
    /// </summary>
    public string FeatureName { get; } = featureName;
}
