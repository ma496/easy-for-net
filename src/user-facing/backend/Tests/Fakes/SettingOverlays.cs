using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Backend.Features.Settings.Core;
using Microsoft.AspNetCore.Http;

namespace Backend.Tests.Fakes;

/// <summary>
/// Setting overrides the test host's setting store reports for particular requests only - the way a
/// test shows what an override does without writing the stored row every other test reads.
/// </summary>
/// <remarks>
/// A stored setting is read by every sign-in, so writing one would reach every test class running
/// beside the one that wrote it. <c>General.IsEmailVerificationRequired</c> makes that concrete: accounts
/// the suite creates are unverified, so a stored row turning it on would refuse sign-ins all over the
/// suite for as long as it stood. An overlay is applied under a token (<see cref="ApplyForRequests"/>): a
/// request carrying that token in <see cref="RequestHeader"/> sees it as the stored overrides, and no
/// other request does.
/// </remarks>
public sealed class SettingOverlays
{
    /// <summary>The header naming the token whose overlays a request sees.</summary>
    public const string RequestHeader = "X-Test-Setting-Overlay";

    private readonly ConcurrentDictionary<(string Token, string Name), string> _requestOverlays = new();

    /// <summary>
    /// Makes every resolution of <paramref name="settingName"/>, made while serving a request whose
    /// <see cref="RequestHeader"/> is <paramref name="token"/>, see <paramref name="values"/> in place of
    /// whatever the stored row holds.
    /// </summary>
    /// <param name="token">A token no other test uses - a fresh <see cref="Guid"/>.</param>
    /// <param name="settingName">The setting's registered name.</param>
    /// <param name="values">The overrides to report.</param>
    public void ApplyForRequests(string token, string settingName, JsonObject values)
        => _requestOverlays[(token, settingName)] = values.ToJsonString();

    internal bool TryGet(string settingName, string? requestToken, out string values)
    {
        values = null!;
        return requestToken is { Length: > 0 } token && _requestOverlays.TryGetValue((token, settingName), out values!);
    }
}

/// <summary>
/// Wraps the real setting store and substitutes the overrides <see cref="SettingOverlays"/> names for a
/// request, passing every other read and every write straight through.
/// </summary>
/// <param name="inner">The store the host would have used.</param>
/// <param name="overlays">What to substitute, and for whom.</param>
/// <param name="httpContextAccessor">The request being served, whose overlay token is read.</param>
sealed class OverlayingSettingValueStore(ISettingValueStore inner, SettingOverlays overlays, IHttpContextAccessor httpContextAccessor) : ISettingValueStore
{
    /// <inheritdoc />
    public async Task<string?> GetValuesAsync(string name, CancellationToken cancellationToken = default)
    {
        var token = httpContextAccessor.HttpContext?.Request.Headers[SettingOverlays.RequestHeader].ToString();
        return overlays.TryGet(name, token, out var values) ? values : await inner.GetValuesAsync(name, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetOwnAsync(string name, string values, CancellationToken cancellationToken = default)
        => inner.SetOwnAsync(name, values, cancellationToken);

    /// <inheritdoc />
    public Task DeleteOwnAsync(string name, CancellationToken cancellationToken = default)
        => inner.DeleteOwnAsync(name, cancellationToken);
}
