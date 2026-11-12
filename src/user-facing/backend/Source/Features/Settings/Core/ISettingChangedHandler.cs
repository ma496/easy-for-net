namespace Backend.Features.Settings.Core;

/// <summary>
/// Told, after the fact, that the stored overrides of a setting were replaced or removed - how another
/// slice drops whatever it cached from a setting's value without the Settings slice knowing it exists.
/// </summary>
/// <remarks>
/// Register an implementation as a singleton (<c>services.AddSingleton&lt;ISettingChangedHandler, …&gt;()</c>);
/// several may be registered and each is told. It is called in the process that made the change, after
/// the change was saved, and must neither throw nor do slow work.
/// </remarks>
[AllowOutside]
public interface ISettingChangedHandler
{
    /// <summary>Reacts to the stored overrides of one setting having changed.</summary>
    /// <param name="settingName">The registered name of the setting that changed.</param>
    void OnSettingChanged(string settingName);
}
