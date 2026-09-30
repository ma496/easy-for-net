namespace Backend.Tests.Fakes;

using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Backend.Features.Settings.Core;

/// <summary>
/// Platform setting overrides the test host's setting store reports for particular tenants only - the
/// way a test shows what a platform-wide override does to a tenant without writing the platform's row.
/// </summary>
/// <remarks>
/// The platform row of a setting is read by every tenant and every sign-in, so writing one would reach
/// every test class running beside the one that wrote it. <c>Signin.IsEmailVerificationRequired</c>
/// makes that concrete: accounts the suite creates are unverified, so a platform row turning it on would
/// refuse sign-ins all over the suite for as long as it stood. A test names only tenants it created, so
/// an overlay applied for one is invisible to every other test. It stands in for the platform row, and
/// the tenant's own row - real, written through the endpoints - still wins over it exactly as it would.
/// </remarks>
public sealed class PlatformSettingOverlays
{
    private readonly ConcurrentDictionary<(Guid TenantId, string Name), string> _overlays = new();

    /// <summary>
    /// Makes resolution for <paramref name="tenantId"/> see <paramref name="platformValues"/> as the
    /// platform's overrides of <paramref name="settingName"/>, in place of whatever the platform row holds.
    /// </summary>
    /// <param name="tenantId">A tenant the test created.</param>
    /// <param name="settingName">The setting's registered name.</param>
    /// <param name="platformValues">The platform overrides to report.</param>
    public void Apply(Guid tenantId, string settingName, JsonObject platformValues)
        => _overlays[(tenantId, settingName)] = platformValues.ToJsonString();

    internal bool TryGet(Guid? tenantId, string settingName, out string platformValues)
    {
        platformValues = null!;
        return tenantId is { } id && _overlays.TryGetValue((id, settingName), out platformValues!);
    }
}

/// <summary>
/// Wraps the real setting store and substitutes the platform layer <see cref="PlatformSettingOverlays"/>
/// names for a tenant, passing every other read and every write straight through.
/// </summary>
/// <param name="inner">The store the host would have used.</param>
/// <param name="overlays">What to substitute, and for whom.</param>
sealed class OverlayingSettingValueStore(ISettingValueStore inner, PlatformSettingOverlays overlays) : ISettingValueStore
{
    /// <inheritdoc />
    public async Task<SettingRows> GetRowsAsync(string name, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await inner.GetRowsAsync(name, tenantId, cancellationToken);
        return overlays.TryGet(tenantId, name, out var platformValues) ? rows with { PlatformValues = platformValues } : rows;
    }

    /// <inheritdoc />
    public Task SetOwnAsync(string name, string values, CancellationToken cancellationToken = default)
        => inner.SetOwnAsync(name, values, cancellationToken);

    /// <inheritdoc />
    public Task DeleteOwnAsync(string name, CancellationToken cancellationToken = default)
        => inner.DeleteOwnAsync(name, cancellationToken);
}