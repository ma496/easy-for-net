namespace Backend.Features.Settings.Core;

/// <summary>
/// Typed reads of the settings the slices declare - the published face of the <c>Settings</c> slice.
/// </summary>
/// <remarks>
/// Every read returns a new instance of the setting class, resolved property by property: the stored
/// override, then the default (the configured one when the setting names a configuration section the
/// deployment supplies, else the class's property initializers). A <see cref="SecretSettingAttribute"/>
/// property is returned decrypted. Values are read from the database on the first read of a setting in
/// a request (or job) and reused for the rest of it; a read in the next request sees whatever was saved
/// in between. Changing a setting revokes no session: a caller that decides something at sign-in or
/// refresh (as <c>GeneralSettings</c> does) reads the value standing at that moment.
/// </remarks>
[AllowOutside]
public interface ISettingProvider
{
    /// <summary>
    /// Resolves <typeparamref name="T"/>. Usable anywhere - a request, anonymous or not, or a job.
    /// </summary>
    /// <typeparam name="T">A setting class some <see cref="ISettingDefinitionProvider"/> registered.</typeparam>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not a registered setting.</exception>
    Task<T> GetAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new();
}

/// <summary>Default implementation of <see cref="ISettingProvider"/>.</summary>
[NoDirectUse]
class SettingProvider(ISettingDefinitionCatalogue catalogue, ISettingValueService settingValueService)
    : ISettingProvider
{
    /// <inheritdoc />
    public async Task<T> GetAsync<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var definition = catalogue.Get(typeof(T));
        var resolved = await settingValueService.ResolveAsync(definition, cancellationToken);
        return (T)definition.Materialize(resolved.Values);
    }
}
