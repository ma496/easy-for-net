namespace Backend.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>GET /roles</c> to return a paginated, searchable list of roles with their permissions and user counts.
/// </summary>
sealed class RoleListEndpoint(IRoleService roleService) : Endpoint<RoleListRequest, RoleListResponse>
{
    public override void Configure()
    {
        Get("");
        Group<RolesGroup>();
        Permissions(Allow.Role_View);
    }

    public override async Task HandleAsync(RoleListRequest request, CancellationToken cancellationToken)
    {
        // get entities from db
        var query = roleService.Roles()
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
/// Request payload for the role list endpoint, supporting search and standard pagination/sort options.
/// </summary>
sealed class RoleListRequest : ListRequestDto<Guid>
{
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
    [MapProperty(nameof(Role.RolePermissions), nameof(RoleGetResponse.Permissions), Use = nameof(RolePermissionsToPermissions)),
     MapProperty(nameof(Role.UserRoles.Count), nameof(RoleGetResponse.UserCount))]
    public partial RoleListDto Map(Role entity);

    private static List<Guid> RolePermissionsToPermissions(ICollection<RolePermission> rolePermissions)
    {
        return [.. rolePermissions.Select(x => x.PermissionId)];
    }
}


