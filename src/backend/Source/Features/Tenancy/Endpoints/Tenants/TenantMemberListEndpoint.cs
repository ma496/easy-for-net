namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>GET /tenants/{tenantId}/members</c> to return a paginated, searchable
/// and role-filterable page of the accounts holding an active membership of one tenant, each carrying
/// the roles it holds in that tenant alone, so the membership screen can show and administer who
/// belongs to a tenant.
/// </summary>
/// <remarks>
/// Marked <see cref="AllowNoTenantAttribute"/> because the tenant being administered travels in the
/// route rather than in the session: a platform administrator acting in no tenant, and a member whose
/// session is active in one of their tenants while they read the members of another, both reach this
/// surface. The route segment names the tenant being read and never the tenant the request acts in,
/// which is why it is authorized explicitly below instead of being trusted: the permission is read
/// for that tenant rather than taken from claims minted for whichever tenant the session happens to
/// be acting in.
/// <para>
/// The rows are produced by <see cref="ITenantAuthorizationService.GetTenantMembersAsync"/> rather
/// than by a query written here: a member's username, email and profile are user-account data owned
/// by the identity slice, which this slice may reach only through the one contract that slice
/// publishes.
/// </para>
/// </remarks>
[AllowNoTenant]
sealed class TenantMemberListEndpoint(ICurrentUserService currentUserService,
                                      ITenantContext tenantContext,
                                      ITenantAuthorizationService tenantAuthorizationService) : Endpoint<TenantMemberListRequest, TenantMemberListResponse>
{
    /// <summary>
    /// The refusal reported to a caller with no standing in the tenant addressed. It is deliberately
    /// the same answer whether that tenant exists or not, so this surface discloses nothing about
    /// tenants the caller has nothing to do with.
    /// </summary>
    private const string NotTenantMemberMessage = "Caller may not view the members of this tenant";

    public override void Configure()
    {
        Get("{tenantId}/members");
        Group<TenantsGroup>();
        Permissions(Allow.TenantMember_View);
    }

    public override async Task HandleAsync(TenantMemberListRequest request, CancellationToken cancellationToken)
    {
        // Standing is checked before the tenant is read at all, and before anything about it reaches
        // the response: a caller who may not view the members of the tenant addressed is refused
        // identically for a tenant that exists and one that does not.
        if (!(currentUserService.IsPlatform() && tenantContext.IsPlatformScope()))
        {
            // Administering any tenant belongs to platform scope, which is where the tenants table is
            // worked from; a platform account that has entered a tenant is an actor of that tenant and
            // is admitted here on the same terms as anybody else. Everyone else is admitted only by
            // holding this permission inside the tenant named in the route - which takes a live
            // membership of it, and which standing in the tenant the session is acting in does not
            // confer.
            var callerId = currentUserService.GetCurrentUserId();
            if (callerId is not { } caller
                || !await tenantAuthorizationService.HoldsTenantPermissionAsync(caller, request.TenantId, Allow.TenantMember_View, cancellationToken))
            {
                ThrowError(NotTenantMemberMessage, ErrorCodes.NotTenantMember);
            }
        }

        // The page, its search, its role filter and its total are all the service's, so the rows carry
        // the roles and the audit values of this tenant's membership alone - the same account read for
        // another tenant reports that other tenant's roles, and neither reading disturbs the other.
        var page = await tenantAuthorizationService.GetTenantMembersAsync(request.TenantId, request, request.RoleId, cancellationToken);

        var dtoMapper = new TenantMemberListDtoMapper();
        var response = new TenantMemberListResponse
        {
            Items = [.. page.Items.Select(dtoMapper.Map)],
            Total = page.Total
        };

        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for the tenant member list endpoint: the tenant whose members are listed, an
/// optional role filter, and the standard search, pagination and sort options.
/// </summary>
sealed class TenantMemberListRequest : ListRequestDto<Guid>
{
    public Guid TenantId { get; set; }
    public Guid? RoleId { get; set; }
}

/// <summary>
/// FluentValidation rules for the tenant member list request, inheriting the standard list-request
/// rules - which bound the page size and cap an unpaged read - and whitelisting the fields the list
/// may be sorted by.
/// </summary>
sealed class TenantMemberListValidator : Validator<TenantMemberListRequest>
{
    public TenantMemberListValidator()
    {
        Include(new ListRequestDtoValidator<Guid>());
        RuleFor(request => request.TenantId).NotEmpty();
        // The page is produced over user accounts, so the whitelist is the account list's own. Sorting
        // reaches the database through reflection over the entity, so a field outside this whitelist
        // has to be refused here: without the rule the request would fail as an unhandled error
        // instead of as a validation failure.
        RuleFor(request => request.SortField)
            .Must(field => string.IsNullOrWhiteSpace(field) ||
                           new[] { "Id", "Username", "Email", "FirstName", "LastName", "IsActive", "CreatedAt", "UpdatedAt" }
                               .Contains(field, StringComparer.OrdinalIgnoreCase))
            .WithMessage("The sort field is not supported.");
    }
}

/// <summary>
/// Response payload for the tenant member list endpoint, wrapping a page of
/// <see cref="TenantMemberListDto"/> items with the total number of members the request matched.
/// </summary>
public sealed class TenantMemberListResponse : ListDto<TenantMemberListDto>
{
}

/// <summary>
/// Per-row DTO representing one member of a tenant. The identity it carries is the member's user
/// account, while the audit values and the roles are the tenant's own record of that account, so the
/// same account listed for two tenants describes each membership independently.
/// </summary>
public sealed class TenantMemberListDto : AuditableDto<Guid>
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; }
    public DateTime MemberSince { get; set; }
    public List<TenantMemberRoleDto> Roles { get; set; } = [];
}

/// <summary>
/// Lightweight DTO exposing a role's id and name for embedding in tenant member list rows. Only roles
/// held inside the tenant being listed appear here.
/// </summary>
public sealed class TenantMemberRoleDto : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
}

/// <summary>
/// This mapper that projects a <see cref="TenantMemberDto"/> - the cross-feature contract's view of a
/// member - onto a <see cref="TenantMemberListDto"/> row, reporting the member's account id as the
/// row identity.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TenantMemberListDtoMapper
{
    [MapProperty(nameof(TenantMemberDto.UserId), nameof(TenantMemberListDto.Id))]
    public partial TenantMemberListDto Map(TenantMemberDto item);
}