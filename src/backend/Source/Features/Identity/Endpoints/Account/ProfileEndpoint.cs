namespace Backend.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Core;

/// <summary>
/// Authenticated GET endpoint that returns the current user's basic profile data
/// (id, username, email, name, and profile image).
/// </summary>
/// <remarks>
/// Marked <see cref="AllowNoTenantAttribute"/> because reading one's own profile is account
/// self-service. The profile - the profile image included - belongs to the account rather than to a
/// tenant's data, so it stays readable while the caller acts in any tenant or in none.
/// </remarks>
[AllowNoTenant]
sealed class ProfileEndpoint(AppDbContext dbContext, ICurrentUserService currentUserService)
    : EndpointWithoutRequest<UserProfileResponse>
{
    public override void Configure()
    {
        Get("profile");
        Group<AccountGroup>();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetCurrentUserId();
        var user = await dbContext.Users
                .Where(x => x.Id == userId)
                .Select(x => new UserProfileResponse
                {
                    Id = x.Id,
                    Username = x.Username,
                    Email = x.Email,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    Image = x.Image,
                })
                .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }
        await Send.ResponseAsync(user, cancellation: cancellationToken);
    }
}

/// <summary>
/// Response payload for the profile endpoint, exposing the user's identity and
/// basic profile fields.
/// </summary>
sealed class UserProfileResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Image { get; set; }
}


