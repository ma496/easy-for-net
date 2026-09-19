namespace Backend.Attributes;

/// <summary>
/// When applied to an endpoint class, this attribute exempts it from the active tenant requirement
/// for a platform account, and for that caller alone. Such a caller runs in the platform scope, where the endpoint answers about the platform's own data - the
/// platform roles, the accounts holding them, and the notifications that belong to no tenant - and
/// what they create is attributed to no tenant; every other caller still needs an active tenant. It
/// marks the user, role, permission and notification endpoints.
/// The exemption covers the tenant requirement only: an endpoint carrying it still requires
/// authentication and still enforces any permissions it declares.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AllowPlatformNoTenantAttribute : Attribute
{
}
