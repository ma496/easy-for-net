namespace Backend.Features.Tenancy.Core.FeatureManagement;

using System.Globalization;
using Backend.Features.Tenancy.Core;

/// <summary>
/// Asks about features for the tenant the current request is acting in.
/// </summary>
/// <remarks>
/// This is the service endpoint and service code should use: it fills in the target from the
/// established tenant scope so a caller states only the feature it cares about. Work that runs outside
/// a request - a queued or scheduled job - has no scope and must use
/// <see cref="IFeatureValueResolver"/>, naming the tenant it acts for.
/// <para>
/// A permission-gated operation needs no check here at all: a permission whose feature is off was
/// never minted into the session, so the endpoint's own <c>Permissions(...)</c> declaration has
/// already refused. Reach for the checker for what permissions do not cover - a numeric limit, or an
/// operation gated by a plan but held by everyone in it.
/// </para>
/// </remarks>
[AllowOutside]
public interface IFeatureChecker
{
    /// <summary>
    /// Whether a feature is enabled for the tenant being acted in, ancestors included.
    /// </summary>
    /// <param name="name">The feature name.</param>
    /// <param name="ct">Token used to cancel the reads.</param>
    /// <returns><see langword="true"/> when the feature is on.</returns>
    Task<bool> IsEnabledAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// The effective value of a feature for the tenant being acted in.
    /// </summary>
    /// <param name="name">The feature name.</param>
    /// <param name="ct">Token used to cancel the reads.</param>
    /// <returns>The value, or <see langword="null"/> when nothing supplied one.</returns>
    Task<string?> GetOrNullAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// The effective value of a feature, converted to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type to read the value as.</typeparam>
    /// <param name="name">The feature name.</param>
    /// <param name="defaultValue">What to return when there is no value, or it will not convert.</param>
    /// <param name="ct">Token used to cancel the reads.</param>
    /// <returns>The converted value, or <paramref name="defaultValue"/>.</returns>
    Task<T> GetAsync<T>(string name, T defaultValue = default, CancellationToken ct = default) where T : struct;

    /// <summary>
    /// Refuses the operation unless the feature is enabled.
    /// </summary>
    /// <param name="name">The feature name.</param>
    /// <param name="ct">Token used to cancel the reads.</param>
    /// <exception cref="FeatureDisabledException">The feature is not enabled.</exception>
    Task CheckEnabledAsync(string name, CancellationToken ct = default);
}

/// <summary>
/// Default <see cref="IFeatureChecker"/> implementation, resolving against the tenant the request
/// established.
/// </summary>
[NoDirectUse]
public class FeatureChecker(IFeatureValueResolver resolver, ITenantContext tenantContext) : IFeatureChecker
{
    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync(string name, CancellationToken ct = default)
        => (await ResolveAsync(ct)).IsEnabled(name);

    /// <inheritdoc/>
    public async Task<string?> GetOrNullAsync(string name, CancellationToken ct = default)
        => (await ResolveAsync(ct)).GetOrNull(name);

    /// <inheritdoc/>
    public async Task<T> GetAsync<T>(string name, T defaultValue = default, CancellationToken ct = default)
        where T : struct
    {
        var value = await GetOrNullAsync(name, ct);
        if (value is null)
        {
            return defaultValue;
        }

        try
        {
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            // A stored value that will not convert is a mistake someone made in the management screen,
            // not a reason to fail the request: the caller asked for a number and gets the fallback it
            // already supplied for the case where nothing was set.
            return defaultValue;
        }
    }

    /// <inheritdoc/>
    public async Task CheckEnabledAsync(string name, CancellationToken ct = default)
    {
        if (!await IsEnabledAsync(name, ct))
        {
            throw new FeatureDisabledException(name);
        }
    }

    /// <summary>
    /// Resolves for the tenant the request established. An unresolved scope is a programming error
    /// rather than the platform: work outside a request has to name the tenant it acts for, and
    /// letting it fall through to the platform would silently answer about the wrong thing.
    /// </summary>
    /// <exception cref="TenantScopeNotEstablishedException">No tenant scope has been established.</exception>
    private Task<FeatureValueSet> ResolveAsync(CancellationToken ct)
    {
        if (!tenantContext.IsResolved)
        {
            throw new TenantScopeNotEstablishedException(
                "Feature values were asked for with no tenant scope established. Work that runs outside a request must resolve them for a named tenant.");
        }
        return resolver.ResolveAsync(new FeatureTarget(tenantContext.CurrentTenantId), ct);
    }
}
