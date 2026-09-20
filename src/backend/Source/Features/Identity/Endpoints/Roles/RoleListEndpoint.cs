namespace Backend.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>GET /roles</c> to return a paginated, searchable list of roles with their permissions and user counts.
/// </summary>
/// <remarks>
/// The rows are the roles of the scope being acted in and no other: the tenant's own inside a tenant,
/// so two tenants may each define a role of the same name without either one appearing in the other's
/// list, search or total, and the platform's own - those belonging to no tenant - in platform scope.
/// A platform account acting in no tenant may additionally name one tenant with the optional tenant
/// filter, which is what lets a role picker offer the roles of the tenant whose members are being
/// administered from the tenants table. For every other caller the filter is ignored rather than
/// honoured, so naming a tenant can never widen a caller's own view.
/// </remarks>
sealed class RoleListEndpoint(IRoleService roleService,
                              ICurrentUserService currentUserService,
                              ITenantContext tenantContext,
                              AppDbContext dbContext) : Endpoint<RoleListRequest, RoleListResponse>
{
    public override void Configure()
    {
        Get("");
        Group<RolesGroup>();
        Permissions(Allow.Role_View);
    }

    public override async Task HandleAsync(RoleListRequest request, CancellationToken cancellationToken)
    {
        // The roles the caller may see: the scope's own - the tenant's inside a tenant, the platform's
        // in platform scope. The search, the tenant filter and the total below all narrow from this one
        // query, so none of them can report a role the caller is not entitled to see.
        // The tenant filter is honoured only for a platform account acting in no tenant, and names the
        // tenant whose roles are wanted directly: the unfiltered list holds the platform roles alone
        // there, while administering a tenant's members from the tenants table still needs that tenant's
        // roles. The tier and the scope are read from the request's own session rather than from
        // anything in the payload, so a caller acting in a tenant who names a tenant is answered from
        // the tenant they are acting in, exactly as if they had named none.
        var roles = request.TenantId is { } tenantId && currentUserService.IsPlatform() && tenantContext.IsPlatformScope()
            ? dbContext.Roles.AcrossAllTenants().Where(x => x.TenantId == tenantId)
            : roleService.Roles();

        var query = roles
            .AsNoTracking()
            .Include(x => x.RolePermissions)
            .Include(x => x.UserRoles)
            .AsQueryable();

        var search = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                EF.Functions.Like(x.NameNormalized, $"%{search}%")
                || EF.Functions.Like(x.Description, $"%{search}%"));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Process(request)
            .ToListAsync(cancellationToken);

        var dtoMapper = new RoleListDtoMapper();
        var response = new RoleListResponse
        {
            Items = [.. items.Select(dtoMapper.Map)],
            Total = total
        };

        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for the role list endpoint, supporting search, standard pagination/sort options,
/// and an optional tenant whose roles are wanted - honoured for a caller holding platform
/// administration and ignored for every other caller.
/// </summary>
sealed class RoleListRequest : ListRequestDto<Guid>
{
    public Guid? TenantId { get; set; }
}

/// <summary>
/// FluentValidation rules for the role list request, inheriting standard list-request validation rules.
/// </summary>
sealed class RoleListValidator : Validator<RoleListRequest>
{
    public RoleListValidator()
    {
        Include(new ListRequestDtoValidator<Guid>());
        RuleFor(request => request.SortField)
            .Must(field => string.IsNullOrWhiteSpace(field) ||
                           new[] { "Id", "Name", "Description", "CreatedAt", "UpdatedAt" }
                               .Contains(field, StringComparer.OrdinalIgnoreCase))
            .WithMessage("The sort field is not supported.");
    }
}

/// <summary>
/// Response payload for the role list endpoint, wrapping a page of <see cref="RoleListDto"/> items with the total count.
/// </summary>
public sealed class RoleListResponse : ListDto<RoleListDto>
{
}

/// <summary>
/// Per-row DTO representing a role in list responses, including its description, permission ids, and user count.
/// </summary>
public sealed class RoleListDto : AuditableDto<Guid>
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string NameNormalized { get; set; } = null!;
    public string? Description { get; set; }
    public List<Guid> Permissions { get; set; } = [];
    public int UserCount { get; set; }
}

/// <summary>
/// This mapper that projects a <see cref="Role"/> entity into a <see cref="RoleListDto"/>, collapsing <see cref="RolePermission"/> join rows into permission ids and computing the user count.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class RoleListDtoMapper
{
    [MapProperty(nameof(Role.RolePermissions), nameof(RoleListDto.Permissions), Use = nameof(RolePermissionsToPermissions)),
     MapProperty(nameof(Role.UserRoles.Count), nameof(RoleListDto.UserCount))]
    public partial RoleListDto Map(Role entity);

    private static List<Guid> RolePermissionsToPermissions(ICollection<RolePermission> rolePermissions)
    {
        return [.. rolePermissions.Select(x => x.PermissionId)];
    }
}


