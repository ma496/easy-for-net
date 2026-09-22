namespace Backend.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>POST /users</c> to create a new user with the supplied roles, and to
/// make that account a member of the tenant the caller is acting in. The sign-in identifiers are kept
/// unique across every tenant, while the roles the account may start with are the acting tenant's own.
/// </summary>
/// <remarks>
/// A caller acting in no tenant runs in platform scope: the account is created as a platform account
/// with no membership, and the roles it may start with are the platform roles - the ones belonging to
/// no tenant. Created from inside a tenant - by a platform account that has entered one just as by that
/// tenant's own administrator - it is an ordinary account of that tenant instead.
/// </remarks>
sealed class UserCreateEndpoint(IUserService userService,
                                ITenantContext tenantContext,
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
        // An account belongs to no tenant - one person authenticates with one account however many
        // tenants they belong to - so both identifier checks read every account there is rather than
        // the ones the caller may administer. A username or an email address already taken in another
        // tenant is therefore taken here too, and the account is refused: a second account carrying
        // the same sign-in identifier could never be told apart from the first at sign-in.
        var usernameExists = await dbContext.Users
            .AnyAsync(x => x.UsernameNormalized == request.Username.Trim().ToLowerInvariant(), cancellationToken);
        if (usernameExists)
        {
            ThrowError("Username already exists", ErrorCodes.UsernameAlreadyExists);
        }

        var emailExists = await dbContext.Users
            .AnyAsync(x => x.EmailNormalized == request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (emailExists)
        {
            ThrowError("Email already exists", ErrorCodes.EmailAlreadyExists);
        }

        // The roles a new account may start with are the roles of the tenant being acted in and no
        // others, so they are read straight through the tenant query filter - the same authority
        // UserUpdateEndpoint reads them from, so a role that can be granted here can also be taken
        // away there. A role belonging to another tenant is not found, exactly as a role that does not
        // exist is not, and both are refused the same way.
        var requestedRoleIds = request.Roles.Distinct().ToList();
        var tenantRoleCount = await dbContext.Roles
            .AsNoTracking()
            .CountAsync(role => requestedRoleIds.Contains(role.Id), cancellationToken);
        if (tenantRoleCount != requestedRoleIds.Count)
        {
            ThrowError(x => x.Roles, "Referenced record does not exist.", ErrorCodes.ReferencedRecordNotFound);
        }

        var requestMapper = new UserCreateRequestMapper();
        var entity = requestMapper.Map(request);
        entity.IsEmailVerified = true;

        // The tier follows the scope the account is created in, and is never taken from the request:
        // creating an account in platform scope is how the platform's own accounts come into being,
        // while one created inside a tenant belongs to that tenant and joins it below. Nothing the
        // caller sends can decide this, so no tenant can mint a platform account for itself.
        entity.IsPlatform = tenantContext.IsPlatformScope();
        // Saving the account also grants it an active membership of the tenant being acted in, written
        // in the same transaction by the service, so an administrator never creates an account that
        // the very next list or read refuses to show them. The membership is attributed centrally from
        // the active tenant rather than from anything the caller sent, so the request cannot name the
        // tenant the new account lands in. Only a platform administrator reaches this with no tenant
        // established, and then the account joins none.
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

