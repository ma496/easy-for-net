namespace Backend.Permissions;

/// <summary>
/// The scope a permission is meaningful in. A session carries only the permissions of the scope it
/// is acting in, so the scope decides where a permission can be exercised at all - never who holds
/// it, which stays a matter of the roles the account is granted.
/// </summary>
/// <remarks>
/// <see cref="Tenant"/> is the default, so a permission declared without naming a scope belongs to
/// the tenant tier and is never offered to a caller acting in platform scope.
/// </remarks>
public enum PermissionScope
{
    /// <summary>
    /// Exercisable only while acting inside a tenant. This is the default for a permission that
    /// names no scope.
    /// </summary>
    Tenant = 0,

    /// <summary>
    /// Exercisable only by a platform account acting in no tenant, where the operation answers about
    /// the platform itself rather than about any one tenant.
    /// </summary>
    Platform = 1,

    /// <summary>
    /// Exercisable in both scopes, answering about the platform's own data in platform scope and
    /// about the tenant's in tenant scope.
    /// </summary>
    Both = 2
}
