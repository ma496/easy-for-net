namespace Backend.Features.Settings.Core;

/// <summary>
/// Marks a property of a setting class as platform-only: a tenant never overrides it, so it resolves
/// from the platform's override, then the default, whichever scope it is read for.
/// </summary>
/// <remarks>
/// Use it for a value that decides something about the whole platform rather than about one tenant -
/// whether anyone may sign up, say, which happens before any tenant exists. <c>PUT /settings/{name}</c>
/// refuses the property with 400 <c>settingPropertyPlatformOnly</c> while acting in a tenant, and
/// resolution skips the tenant layer for it, so a tenant row that holds it anyway (written by hand, or
/// before the property was marked) has no effect: its source is never <c>tenant</c>. The catalogue
/// refuses, at startup, the attribute on a property that is also a <see cref="SecretSettingAttribute"/>.
/// </remarks>
[AllowOutside]
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class PlatformSettingAttribute : Attribute
{
}
