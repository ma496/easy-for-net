using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

namespace Backend.Features.Identity.Endpoints.Users;

/// <summary>
/// This endpoint that handles <c>POST /users</c> to create a new user with the supplied roles.
/// </summary>
sealed class UserCreateEndpoint(IUserService userService,
                                AppDbContext dbContext) : Endpoint<UserCreateRequest, UserCreateResponse>
{
    public override void Configure()
    {
        Post("");
        Group<UsersGroup>();
        Permissions(Allow.User_Create);
    }

    public override async Task HandleAsync(UserCreateRequest request, CancellationToken cancellationToken)
    {
        var usernameExists = await dbContext.Users
            .AnyAsync(x => x.UsernameNormalized == request.Username.Trim().ToLowerInvariant(), cancellationToken);
        if (usernameExists)
        {
            this.ThrowError(ErrorCodes.UsernameAlreadyExists);
        }

        var emailExists = await dbContext.Users
            .AnyAsync(x => x.EmailNormalized == request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (emailExists)
        {
            this.ThrowError(ErrorCodes.EmailAlreadyExists);
        }

        var requestedRoleIds = request.Roles.Distinct().ToList();
        var roleCount = await dbContext.Roles
            .AsNoTracking()
            .CountAsync(role => requestedRoleIds.Contains(role.Id), cancellationToken);
        if (roleCount != requestedRoleIds.Count)
        {
            this.ThrowError(x => x.Roles, ErrorCodes.ReferencedRecordNotFound);
        }

        var requestMapper = new UserCreateRequestMapper();
        var entity = requestMapper.Map(request);
        entity.IsEmailVerified = true;

        await userService.CreateAsync(entity, request.Password);

        var responseMapper = new UserCreateResponseMapper();
        await Send.ResponseAsync(responseMapper.Map(entity), cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for creating a new user, including the initial password and role assignments.
/// </summary>
public sealed class UserCreateRequest
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; }
    public List<Guid> Roles { get; set; } = [];
}

/// <summary>
/// FluentValidation rules ensuring a create-user request supplies a unique username, valid email, strong password, and at least one role.
/// </summary>
sealed class UserCreateValidator : Validator<UserCreateRequest>
{
    public UserCreateValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3).MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(50);
        RuleFor(x => x.FirstName).MinimumLength(3).MaximumLength(50).When(x => !x.FirstName.IsNullOrEmpty());
        RuleFor(x => x.LastName).MinimumLength(3).MaximumLength(50).When(x => !x.LastName.IsNullOrEmpty());
        RuleFor(x => x.Roles).NotEmpty();
    }
}

/// <summary>
/// Response payload returned after a successful user creation, echoing the assigned identifiers and roles.
/// </summary>
public sealed class UserCreateResponse : BaseDto<Guid>
{
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
/// This mapper that projects a <see cref="UserCreateRequest"/> into a <see cref="User"/> entity, expanding role ids into <see cref="UserRole"/> join rows.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Source)]
public partial class UserCreateRequestMapper
{
    [MapProperty("Roles", "UserRoles", Use = nameof(RolesToUserRoles)),
     MapperIgnoreSource(nameof(UserCreateRequest.Password))]
    public partial User Map(UserCreateRequest request);

    // A UserRole is keyed by the pair of account and role, so one request naming the same role twice
    // would produce two rows carrying one key and fail the save rather than the request. The ids are
    // collapsed to the set the endpoint validated, which is the set the response then echoes back.
    private static ICollection<UserRole> RolesToUserRoles(List<Guid> roles)
        => [.. roles.Distinct().Select(x => new UserRole { RoleId = x })];
}

/// <summary>
/// This mapper that projects a <see cref="User"/> entity into a <see cref="UserCreateResponse"/>, collapsing <see cref="UserRole"/> join rows back into role ids.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class UserCreateResponseMapper
{
    [MapProperty("UserRoles", "Roles", Use = nameof(UserRolesToRoles))]
    public partial UserCreateResponse Map(User entity);

    private static List<Guid> UserRolesToRoles(ICollection<UserRole> userRoles)
        => [.. userRoles.Select(x => x.RoleId)];
}

