namespace Backend.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>GET /users</c> to return a paginated, filterable list of the user accounts the caller may administer, with their role assignments.
/// </summary>
sealed class UserListEndpoint(IUserService userService) : Endpoint<UserListRequest, UserListResponse>
{
    public override void Configure()
    {
        Get("");
        Group<UsersGroup>();
        Permissions(Allow.User_View);
    }

    public override async Task HandleAsync(UserListRequest request, CancellationToken cancellationToken)
    {
        // The accounts the caller may administer: those holding an active membership of the tenant
        // being acted in, widened to every account for a platform administrator. The search, the
        // filters and the total below all narrow from this one query, so none of them can report an
        // account the caller is not entitled to see.
        var query = userService.TenantUsers()
            .AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .AsQueryable();

        var search = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                EF.Functions.Like(x.UsernameNormalized, $"%{search}%")
                || EF.Functions.Like(x.EmailNormalized, $"%{search}%")
                || EF.Functions.Like(x.FirstName, $"%{search}%")
                || EF.Functions.Like(x.LastName, $"%{search}%"));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == request.IsActive.Value);
        }

        if (request.RoleId.HasValue)
        {
            query = query.Where(x => x.UserRoles.Any(ur => ur.RoleId == request.RoleId.Value));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Process(request)
            .ToListAsync(cancellationToken);

        var dtoMapper = new UserListDtoMapper();
        var response = new UserListResponse
        {
            Items = [.. items.Select(dtoMapper.Map)],
            Total = total
        };

        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for the user list endpoint, supporting search, active/role filtering, and standard pagination/sort options.
/// </summary>
sealed class UserListRequest : ListRequestDto<Guid>
{
    public bool? IsActive { get; set; }
    public Guid? RoleId { get; set; }
}

/// <summary>
/// FluentValidation rules for the user list request, inheriting standard list-request validation rules.
/// </summary>
sealed class UserListValidator : Validator<UserListRequest>
{
    public UserListValidator()
    {
        Include(new ListRequestDtoValidator<Guid>());
        RuleFor(request => request.SortField)
            .Must(field => string.IsNullOrWhiteSpace(field) ||
                           new[] { "Id", "Username", "Email", "FirstName", "LastName", "IsActive", "CreatedAt", "UpdatedAt" }
                               .Contains(field, StringComparer.OrdinalIgnoreCase))
            .WithMessage("The sort field is not supported.");
    }
}

/// <summary>
/// Response payload for the user list endpoint, wrapping a page of <see cref="UserListDto"/> items with the total count.
/// </summary>
public sealed class UserListResponse : ListDto<UserListDto>
{
}

/// <summary>
/// Per-row DTO representing a user in list responses, including profile fields and a compact role summary.
/// </summary>
public sealed class UserListDto : AuditableDto<Guid>, ISystemCreatedDto
{
    public bool SystemCreated { get; set; }
    public string Username { get; set; } = null!;
    public string UsernameNormalized { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string EmailNormalized { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; }
    public List<UserRoleDto> Roles { get; set; } = [];
}

/// <summary>
/// Lightweight DTO exposing a role's id and name for embedding in user list rows.
/// </summary>
public sealed class UserRoleDto : BaseDto<Guid>
{
    public string Name { get; set; } = null!;
}

/// <summary>
/// This mapper that projects a <see cref="User"/> entity into a <see cref="UserListDto"/>, collapsing <see cref="UserRole"/> join rows into <see cref="UserRoleDto"/> entries.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class UserListDtoMapper
{
    [MapProperty("UserRoles", "Roles", Use = nameof(UserRolesToRoles))]
    public partial UserListDto Map(User entity);

    // A role of another tenant is filtered out of the include, which leaves its assignment row with
    // no role attached; such an assignment names nothing the caller may see, so it is not reported.
    private static List<UserRoleDto> UserRolesToRoles(ICollection<UserRole> userRoles)
        => [.. userRoles.Where(x => x.Role != null).Select(x => new UserRoleDto { Id = x.RoleId, Name = x.Role.Name })];
}
