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
        // get entity from db
        var entity = await userService.Users()
            .AsNoTracking()
            .Include(x => x.UserRoles)
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
public sealed class UserGetResponse : AuditableDto<Guid>
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

    private static List<Guid> UserRolesToRoles(ICollection<UserRole> userRoles)
        => [.. userRoles.Select(x => x.RoleId)];
}