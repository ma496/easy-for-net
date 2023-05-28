using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ardalis.GuardClauses;
using CSharpTemplate.Common.Context;
using CSharpTemplate.Common.Identity.Dto;
using CSharpTemplate.Common.Identity.Entities;
using CSharpTemplate.Common.Identity.Permissions;
using EasyForNet.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CSharpTemplate.Common.Identity;

public class AuthManager : DomainService, IAuthManager
{
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly CSharpTemplateDbContextBase _dbContext;
    private readonly IUserManager _userManager;
    private readonly IPermissionManager _permissionManager;

    public AuthManager(IPasswordHasher<User> passwordHasher, IConfiguration configuration, 
        CSharpTemplateDbContextBase dbContext, IUserManager userManager, IPermissionManager permissionManager)
    {
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _dbContext = dbContext;
        _userManager = userManager;
        _permissionManager = permissionManager;
    }

    public async Task RegisterUserAsync(RegisterUserInput input)
    {
        Guard.Against.Null(input, nameof(input));

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        var user = await _userManager.CreateAsync(new UserDto
        {
            Username = input.Username,
            Email = input.Email,
            Name = input.Name
        });
        await _userManager.UpdatePasswordAsync(user, input.Password);

        await transaction.CommitAsync();
    }

    public async Task<LoginUserOutput> LoginUserAsync(LoginUserInput input)
    {
        Guard.Against.Null(input, nameof(input));

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => string.Equals(u.Email.ToLower(), input.Email.ToLower()));

        if (user == null)
        {
            return new LoginUserOutput
            {
                Message = "There is no user with that Email address",
                IsSuccess = false,
            };
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.HashedPassword, input.Password);

        if (result == PasswordVerificationResult.Success)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, input.Email),
                new Claim(ClaimTypes.Name, user.Username),
            };

            var keyStr = _configuration["AuthSettings:Key"];
            Guard.Against.NullOrWhiteSpace(keyStr, nameof(keyStr));
            
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));

            var token = new JwtSecurityToken(
                issuer: _configuration["AuthSettings:Issuer"], 
                audience: _configuration["AuthSettings:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(30),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            var tokenAsString = new JwtSecurityTokenHandler().WriteToken(token);

            await _permissionManager.SetPermissions(user.Id);

            return new LoginUserOutput
            {
                Message = "User login successfully!",
                Token = tokenAsString,
                IsSuccess = true,
                ExpireDate = token.ValidTo
            };
        }

        return new LoginUserOutput
        {
            Message = "Password dont match",
            IsSuccess = false
        };
    }
}