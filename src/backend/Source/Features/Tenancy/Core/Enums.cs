namespace Backend.Features.Tenancy.Core;

/// <summary>
/// Lifecycle state of a tenant. Deletion is not a status: a deleted tenant is a soft-deleted row
/// rather than a state named here.
/// </summary>
/// <remarks>
/// Published to the rest of the application rather than kept inside the slice, because a tenant's
/// lifecycle is the one thing other features have to be able to read off a tenant - a file is refused
/// in a suspended tenant, and the account-info response reports the state of every tenant it lists.
/// Reporting it as a boolean, or as a second copy of the enum, would let the two drift; the tenant
/// row itself stays private to this feature.
/// </remarks>
[AllowOutside]
public enum TenantStatus
{
    Active = 1,
    Suspended = 2
}
