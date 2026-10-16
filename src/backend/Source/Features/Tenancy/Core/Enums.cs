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
    Active,
    Suspended
}

/// <summary>
/// What became of a membership change that the last-administrator guard, or the membership's own
/// absence, may refuse. Neither refusal is an exception, because both are ordinary answers rather
/// than faults; the caller turns each value into what the requester sees.
/// </summary>
public enum TenantMembershipChangeOutcome
{
    /// <summary>
    /// The change was persisted.
    /// </summary>
    Applied,

    /// <summary>
    /// The account holds no membership of that tenant, so there was nothing to change. The caller
    /// reports this as a missing record: a membership is a record inside a tenant, and one the caller
    /// cannot see is indistinguishable from one that never existed.
    /// </summary>
    MembershipNotFound,

    /// <summary>
    /// The change would have left the tenant with no member holding tenant administration and was
    /// refused; nothing was persisted. The caller reports it with
    /// <see cref="ErrorCodes.LastTenantAdministrator"/>.
    /// </summary>
    LastTenantAdministrator
}
