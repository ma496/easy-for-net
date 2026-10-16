using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Backend.Features.Settings.Core;
using Microsoft.AspNetCore.Http;

namespace Backend.Tests.Fakes;

/// <summary>
/// Platform setting overrides the test host's setting store reports for particular tenants, or for
/// particular requests, only - the way a test shows what a platform-wide override does without writing
/// the platform's row.
/// </summary>
/// <remarks>
/// The platform row of a setting is read by every tenant and every sign-in, so writing one would reach
/// every test class running beside the one that wrote it. <c>Signin.IsEmailVerificationRequired</c>
/// makes that concrete: accounts the suite creates are unverified, so a platform row turning it on would
/// refuse sign-ins all over the suite for as long as it stood. A test names only tenants it created, so
/// an overlay applied for one is invisible to every other test. It stands in for the platform row, and
/// the tenant's own row - real, written through the endpoints - still wins over it exactly as it would.
/// <para>
/// Code that reads the platform's value itself (<c>GetAsync&lt;T&gt;(null)</c>, as sign-up and the account
/// emails do) names no tenant to key on, so an overlay may instead be applied under a token
/// (<see cref="ApplyForRequests"/>): a request carrying that token in <see cref="RequestHeader"/> sees it as
/// the platform's overrides, and no other request does.
/// </para>
/// </remarks>
public sealed class PlatformSettingOverlays
{
    /// <summary>The header naming the token whose request overlays a request sees.</summary>
    public const string RequestHeader = "X-Test-Platform-Setting-Overlay";

    private readonly ConcurrentDictionary<(Guid TenantId, string Name), string> _overlays = new();
    private readonly ConcurrentDictionary<(string Token, string Name), string> _requestOverlays = new();

    /// <summary>
    /// Makes resolution for <paramref name="tenantId"/> see <paramref name="platformValues"/> as the
    /// platform's overrides of <paramref name="settingName"/>, in place of whatever the platform row holds.
    /// </summary>
    /// <param name="tenantId">A tenant the test created.</param>
    /// <param name="settingName">The setting's registered name.</param>
    /// <param name="platformValues">The platform overrides to report.</param>
    public void Apply(Guid tenantId, string settingName, JsonObject platformValues)
        => _overlays[(tenantId, settingName)] = platformValues.ToJsonString();

    /// <summary>
    /// Makes every resolution of the platform's own value of <paramref name="settingName"/>, made while
    /// serving a request whose <see cref="RequestHeader"/> is <paramref name="token"/>, see
    /// <paramref name="platformValues"/> in place of whatever the platform row holds.
    /// </summary>
    /// <param name="token">A token no other test uses - a fresh <see cref="Guid"/>.</param>
    /// <param name="settingName">The setting's registered name.</param>
    /// <param name="platformValues">The platform overrides to report.</param>
    public void ApplyForRequests(string token, string settingName, JsonObject platformValues)
        => _requestOverlays[(token, settingName)] = platformValues.ToJsonString();

    internal bool TryGet(Guid? tenantId, string settingName, string? requestToken, out string platformValues)
    {
        platformValues = null!;
        if (tenantId is { } id)
        {
            return _overlays.TryGetValue((id, settingName), out platformValues!);
        }

        return requestToken is { Length: > 0 } token && _requestOverlays.TryGetValue((token, settingName), out platformValues!);
    }
}

/// <summary>
/// Wraps the real setting store and substitutes the platform layer <see cref="PlatformSettingOverlays"/>
/// names for a tenant or a request, passing every other read and every write straight through.
/// </summary>
/// <param name="inner">The store the host would have used.</param>
/// <param name="overlays">What to substitute, and for whom.</param>
/// <param name="httpContextAccessor">The request being served, whose overlay token is read.</param>
sealed class OverlayingSettingValueStore(ISettingValueStore inner, PlatformSettingOverlays overlays, IHttpContextAccessor httpContextAccessor) : ISettingValueStore
{
    /// <inheritdoc />
    public async Task<SettingRows> GetRowsAsync(string name, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await inner.GetRowsAsync(name, tenantId, cancellationToken);
        var token = httpContextAccessor.HttpContext?.Request.Headers[PlatformSettingOverlays.RequestHeader].ToString();
        return overlays.TryGet(tenantId, name, token, out var platformValues) ? rows with { PlatformValues = platformValues } : rows;
    }

    /// <inheritdoc />
    public Task SetOwnAsync(string name, string values, CancellationToken cancellationToken = default)
        => inner.SetOwnAsync(name, values, cancellationToken);

    /// <inheritdoc />
    public Task DeleteOwnAsync(string name, CancellationToken cancellationToken = default)
        => inner.DeleteOwnAsync(name, cancellationToken);
}
