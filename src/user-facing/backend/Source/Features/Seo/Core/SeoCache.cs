using Backend.Features.Settings.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Backend.Features.Seo.Core;

/// <summary>
/// The in-process cache of resolved page metadata. Every entry is tied to one change token, so a single
/// <see cref="Invalidate"/> - after any SEO page write, or any change to the <c>Seo</c> setting - drops
/// them all at once.
/// </summary>
/// <remarks>
/// Each API instance holds its own cache, so behind several instances a change reaches the others when
/// their entries expire (<see cref="SeoCache.Lifetime"/>).
/// </remarks>
public interface ISeoCache
{
    /// <summary>Returns the cached value for <paramref name="key"/>, or creates, caches and returns it.</summary>
    /// <typeparam name="T">The cached value's type.</typeparam>
    /// <param name="key">The entry's key within the SEO cache.</param>
    /// <param name="factory">Produces the value when it is not cached.</param>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory) where T : class;

    /// <summary>Drops every cached entry.</summary>
    void Invalidate();
}

/// <summary>
/// Default <see cref="ISeoCache"/> over <see cref="IMemoryCache"/>, and the
/// <see cref="ISettingChangedHandler"/> that drops it when the <c>Seo</c> setting changes.
/// </summary>
[NoDirectUse]
public sealed class SeoCache(IMemoryCache memoryCache) : ISeoCache, ISettingChangedHandler, IDisposable
{
    /// <summary>The longest an entry is kept, however quiet the site is.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly Lock _lock = new();
    private CancellationTokenSource _generation = new();

    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory) where T : class
    {
        var cacheKey = $"seo:{key}";
        if (memoryCache.TryGetValue(cacheKey, out T? cached) && cached is not null)
        {
            return cached;
        }

        // The token is taken before the value is read, so a write landing during the read invalidates
        // the entry it would otherwise have cached stale.
        IChangeToken token;
        lock (_lock)
        {
            token = new CancellationChangeToken(_generation.Token);
        }

        var value = await factory();
        using (var entry = memoryCache.CreateEntry(cacheKey))
        {
            entry.Value = value;
            entry.AbsoluteExpirationRelativeToNow = Lifetime;
            entry.ExpirationTokens.Add(token);
        }

        return value;
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        CancellationTokenSource previous;
        lock (_lock)
        {
            previous = _generation;
            _generation = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
    }

    /// <inheritdoc />
    public void OnSettingChanged(string settingName)
    {
        if (string.Equals(settingName, SeoSettingsProvider.SeoSettingName, StringComparison.OrdinalIgnoreCase))
        {
            Invalidate();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            _generation.Dispose();
        }
    }
}
