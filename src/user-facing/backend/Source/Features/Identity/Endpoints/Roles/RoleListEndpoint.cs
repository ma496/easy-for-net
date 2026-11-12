using Backend.Features.Identity.Core;

namespace Backend.Features.Identity.Endpoints.Roles;

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
        var query = roleService.Roles().AsNoTracking();

        var search = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                EF.Functions.Like(x.NameNormalized, $"%{search}%")
                || EF.Functions.Like(x.Description, $"%{search}%"));
        }

        var total = await query.CountAsync(cancellationToken);
        // Sorting and paging run on the roles themselves, which is what Process works over; the page is
        // then projected, so only the columns a row reports are read and the holders are counted in the
        // database rather than loaded.
        var items = await query
            .Process(request)
            .Select(x => new RoleListDto
            {
                Id = x.Id,
                CreatedAt = x.CreatedAt,
                CreatedBy = x.CreatedBy,
                UpdatedAt = x.UpdatedAt,
                UpdatedBy = x.UpdatedBy,
                SystemCreated = x.SystemCreated,
                Name = x.Name,
                NameNormalized = x.NameNormalized,
                Description = x.Description,
                Permissions = x.RolePermissions.Select(rolePermission => rolePermission.PermissionId).ToList(),
                UserCount = x.UserRoles.Count
            })
            .ToListAsync(cancellationToken);

        var response = new RoleListResponse
        {
            Items = items,
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
public sealed class RoleListDto : AuditableDto<Guid>, ISystemCreatedDto
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string NameNormalized { get; set; } = null!;
    public string? Description { get; set; }
    public List<Guid> Permissions { get; set; } = [];
    public int UserCount { get; set; }
}


