namespace Backend.Attributes;

/// <summary>
/// When applied to an endpoint class, this attribute exempts it from the active tenant
/// requirement, so the endpoint runs even when no tenant is established for the request.
/// It marks the anonymous and the authenticated account self-service flows, the tenant
/// lifecycle endpoints and the account-owned file endpoints, which either need no tenant
/// at all or resolve the tenant they act on themselves.
/// The exemption covers the tenant requirement only: an endpoint carrying it still
/// requires authentication and still enforces any permissions it declares.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AllowNoTenantAttribute : Attribute
{
}