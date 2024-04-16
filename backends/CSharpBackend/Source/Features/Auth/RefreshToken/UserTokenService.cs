using Efn.Constants;
using Efn.Infrastructure.EfPersistence;
using Efn.Infrastructure.Services;
using FastEndpoints.Security;
using Microsoft.EntityFrameworkCore;

namespace Efn.Features.Auth.RefreshToken;

public class UserTokenService : RefreshTokenService<TokenRequest, MyTokenResponse>
{
    readonly AppDbContext _dbContext;
    readonly IDateTime _dateTime;

    public UserTokenService(IConfiguration config, AppDbContext dbContext, IDateTime dateTime)
    {
        _dbContext = dbContext;
        _dateTime = dateTime;
        Setup(x =>
        {
            x.TokenSigningKey = config["JWTSigningKey"];
            x.AccessTokenValidity = TimeSpan.FromMinutes(10);
            x.RefreshTokenValidity = TimeSpan.FromHours(1);
            x.Endpoint("auth/refresh-token", ep =>
            {
                ep.Summary(s => s.Description = "this is the refresh token endpoint");
            });
        });
    }

    public override async Task PersistTokenAsync(MyTokenResponse rsp)
    {
        // call on sign-in and refresh token endpoint
        var refreshToken = await _dbContext.RefreshTokens
            .Where(e => e.UserId.ToString() == rsp.UserId)
            .FirstOrDefaultAsync();
        if (refreshToken != null)
        {
            _dbContext.RefreshTokens.Remove(refreshToken);
            await _dbContext.SaveChangesAsync();
        }

        var newRefreshToken = new Identity.Entities.RefreshToken
        {
            UserId = Guid.Parse(rsp.UserId),
            ExpiryDate = rsp.RefreshExpiry,
            Token = rsp.RefreshToken
        };
        _dbContext.RefreshTokens.Add(newRefreshToken);

        await _dbContext.SaveChangesAsync();
    }

    public override async Task RefreshRequestValidationAsync(TokenRequest req)
    {
        // call on refresh token endpoint
        var token = await _dbContext.RefreshTokens
            .Where(e =>
                e.UserId == Guid.Parse(req.UserId)
                && e.Token == req.RefreshToken)
            .OrderByDescending(e => e.ExpiryDate)
            .FirstOrDefaultAsync();
        if (token == null || token.ExpiryDate < _dateTime.Now)
            AddError("The refresh token is not valid!");
    }

    public override async Task SetRenewalPrivilegesAsync(TokenRequest request, UserPrivileges privileges)
    {
        // call on refresh token endpoint
        await Task.Delay(100); //simulate a db call
        privileges.Claims.Add(new("userId", request.UserId));
        privileges.Permissions.AddRange(new Allow().AllCodes());
    }
}
