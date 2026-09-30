namespace Backend.Features.Settings.Core;

using Backend.Features.Tenancy.Core;

/// <summary>
/// Typed reads of the settings the slices declare - the published face of the <c>Settings</c> slice.
/// </summary>
/// <remarks>
/// Every read returns a new instance of the setting class, resolved property by property: the tenant's
/// own override, then the platform's, then the class's property initializers. Values are read from the
/// database on the first read of a setting in a request (or job) and reused for the rest of it; a read
/// in the next request sees whatever was saved in between. Changing a setting revokes no session: a
/// caller that decides something at sign-in or refresh (as <c>SigninSettings</c> does) reads the value
/// standing at that moment.
/// </remarks>
[AllowOutside]
public interface ISettingProvider
{
    /// <summary>
    /// Resolves <typeparamref name="T"/> for the acting scope: the tenant the request or job acts in, or
    /// the platform in platform scope. Refuses, as <see cref="ITenantContext.CurrentTenantId"/> does,
    /// when no scope has been established - an anonymous caller names its target with the other overload.
    /// </summary>
    /// <typeparam name="T">A setting class some <see cref="ISettingDefinitionProvider"/> registered.</typeparam>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not a registered setting.</exception>
    Task<T> GetAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>
    /// Resolves <typeparamref name="T"/> for an explicit target: <paramref name="tenantId"/>'s value, or the
    /// platform's (default with the platform's overrides) when it is <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">A setting class some <see cref="ISettingDefinitionProvider"/> registered.</typeparam>
    /// <param name="tenantId">The tenant to resolve for, or <see langword="null"/> for the platform.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not a registered setting.</exception>
    Task<T> GetAsync<T>(Guid? tenantId, CancellationToken cancellationToken = default)
        where T : class, new();
}

/// <summary>Default implementation of <see cref="ISettingProvider"/>.</summary>
[NoDirectUse]
class SettingProvider(ISettingDefinitionCatalogue catalogue, ISettingValueService settingValueService, ITenantContext tenantContext)
    : ISettingProvider
{
    /// <inheritdoc />
    public Task<T> GetAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
        => GetAsync<T>(tenantContext.CurrentTenantId, cancellationToken);

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(Guid? tenantId, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var definition = catalogue.Get(typeof(T));
        var resolved = await settingValueService.ResolveAsync(definition, tenantId, cancellationToken);
        return (T)definition.Materialize(resolved.Values);
    }
}