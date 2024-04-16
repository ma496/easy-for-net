using Efn.Constants;
using Efn.Features.Auth.RefreshToken;
using Efn.Infrastructure.EfPersistence;
using Microsoft.EntityFrameworkCore;

namespace Efn.Features.Auth.SignIn;

sealed class Endpoint : Endpoint<Request, MyTokenResponse>
{
    readonly AppDbContext _dbContext;

    public Endpoint(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Post("auth/sign-in");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request r, CancellationToken c)
    {
        var userId = await GetUserId(r.Username, r.Password);

        if (userId == default)
            ThrowError("Invalid user credentials!");

        Response = await CreateTokenWith<UserTokenService>(userId.ToString(), p =>
        {
            p.Claims.Add(new("userId", userId.ToString()));
            p.Permissions.AddRange(new Allow().AllCodes());
        });
    }

    private async Task<Guid> GetUserId(string username, string password)
    {
        var userId = await _dbContext.Users
            .Where(e => e.Username == username && e.Password == password)
            .Select(e => e.Id)
            .SingleOrDefaultAsync();
        return userId;
    }
}