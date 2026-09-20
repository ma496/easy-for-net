namespace Backend.Tenancy;

/// <summary>
/// Session material returned when an active tenant is selected or switched. It carries exactly
/// what the sign-in response carries, so a caller replaces its
/// token pair with a session bound to the newly selected tenant while staying authenticated and
/// re-entering no credentials. Cookie-authenticated clients ignore it.
/// The member names are this contract's own: the framework token response spells the two expiries
/// <c>AccessExpiry</c> and <c>RefreshExpiry</c> and carries the user id as a string, so whatever
/// issues the pair projects onto these members instead of assigning across.
/// </summary>
public sealed class TenantSessionDto
{
    public Guid UserId { get; set; }
    public string AccessToken { get; set; } = null!;
    public DateTime AccessTokenExpiry { get; set; }
    public string RefreshToken { get; set; } = null!;
    public DateTime RefreshTokenExpiry { get; set; }
}

/// <summary>
/// A single account holding an active membership of one tenant, together with the roles it is
/// assigned in that tenant alone, so that the same account described for another tenant
/// carries that other tenant's roles and audit values independently.
/// </summary>
public sealed class TenantMemberDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; }
    public DateTime MemberSince { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public List<TenantMemberRoleInfo> Roles { get; set; } = [];
}

/// <summary>
/// Identifier and name of a role assigned to a member within the tenant the containing
/// <see cref="TenantMemberDto"/> describes.
/// </summary>
public sealed class TenantMemberRoleInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}

/// <summary>
/// One page of tenant members, carrying the rows for the requested page and the total number of
/// members matching the request across all pages through <see cref="ListDto{T}"/>, the wrapper
/// every list response here uses. The page number and page size are the caller's,
/// taken from the <c>ListRequestDto</c> the request carries and bounded there to 1..100, and
/// <c>Total</c> is counted before paging so a caller can tell how many pages remain.
/// </summary>
public sealed class TenantMemberPageDto : ListDto<TenantMemberDto>
{
}