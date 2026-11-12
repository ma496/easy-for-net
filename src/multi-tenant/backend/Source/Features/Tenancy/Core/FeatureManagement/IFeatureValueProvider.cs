namespace Backend.Features.Tenancy.Core.FeatureManagement;

/// <summary>
/// One link in the chain that decides what a feature is worth for a given target.
/// </summary>
/// <remarks>
/// A provider answers in bulk rather than one feature at a time, because every caller wants the whole
/// catalogue at once - a session mint asks about every permission-gating feature, and the management
/// screen asks about all of them. Answering per feature would turn one indexed read into dozens.
/// </remarks>
public interface IFeatureValueProvider
{
    /// <summary>
    /// The provider's name, from <see cref="FeatureValueProviderNames"/>. It is recorded against each
    /// resolved value so the management UI can say where an inherited value came from.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Returns every value this provider holds for <paramref name="target"/>, by feature name.
    /// </summary>
    /// <param name="target">Who the values are being resolved for.</param>
    /// <param name="ct">Token used to cancel the read.</param>
    /// <returns>The values this provider supplies, which may be empty.</returns>
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(FeatureTarget target, CancellationToken ct = default);
}
