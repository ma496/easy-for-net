namespace Backend.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// This endpoint that handles <c>GET /users/{id}</c> to return a single user with their role assignments.
/// </summary>
sealed class UserGetEndpoint(IUserService userService) : Endpoint<UserGetRequest, UserGetResponse>
{
    public override void Configure()
    {
        Get("{id}");
        Group<UsersGroup>();
        Permissions(Allow.User_View);
    }

    public override async Task HandleAsync(UserGetRequest request, CancellationToken cancellationToken)
    {
        // The account is read from the set the caller may administer, so one holding no membership of
        // the tenant being acted in is simply not there and answers exactly as an account that does not
        // exist does. A platform administrator reads every account irrespective of membership.
        var entity = await userService.TenantUsers()
            .AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        await Send.ResponseAsync(new UserGetResponseMapper().Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload identifying the user to fetch by id.
/// </summary>
sealed class UserGetRequest : BaseDto<Guid>
{
}

/// <summary>
/// FluentValidation rules requiring a non-empty id for user retrieval.
/// </summary>
sealed class UserGetValidator : Validator<UserGetRequest>
{
    public UserGetValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

/// <summary>
/// Response payload containing the user's profile fields, audit metadata, and assigned role ids.
/// </summary>
public sealed class UserGetResponse : AuditableDto<Guid>, ISystemCreatedDto
{
    public bool SystemCreated { get; set; }
    public string Username { get; set; } = null!;
    public string UsernameNormalized { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string EmailNormalized { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; }

    public List<Guid> Roles { get; set; } = [];
}

/// <summary>
/// This mapper that projects a <see cref="User"/> entity into a <see cref="UserGetResponse"/>, collapsing <see cref="UserRole"/> join rows into role ids.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class UserGetResponseMapper
{
    [MapProperty("UserRoles", "Roles", Use = nameof(UserRolesToRoles))]
    public partial UserGetResponse Map(User entity);

    // Only the roles the caller may see are reported: an assignment to a role of another tenant comes
    // back with no role attached, and naming it here would offer the update endpoint a role it refuses.
    private static List<Guid> UserRolesToRoles(ICollection<UserRole> userRoles)
        => [.. userRoles.Where(x => x.Role != null).Select(x => x.RoleId)];
}