using Ardalis.GuardClauses;
using EasyForNet.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CSharpTemplate.Common.Context;
using CSharpTemplate.Common.Identity.Entities;
using Microsoft.EntityFrameworkCore;

namespace CSharpTemplate.Common.Identity;

public class AuthManager : DomainService, IAuthManager
{
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly CSharpTemplateDbContextBase _dbContext;

    public AuthManager(IPasswordHasher<AppUser> passwordHasher, IConfiguration configuration, 
        CSharpTemplateDbContextBase dbContext)
    {
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _dbContext = dbContext;
    }

    public async Task<RegisterUserOutput> RegisterUserAsync(RegisterUserInput input)
    {
        Guard.Against.Null(input, nameof(input));

        try
        {
            var transaction = await _dbContext.Database.BeginTransactionAsync();
            var entry = await _dbContext.Users.AddAsync(new AppUser
            {
                Username = input.Email,
                Email = input.Email
            });
            await _dbContext.SaveChangesAsync();

            entry.Entity.HashedPassword = _passwordHasher.HashPassword(entry.Entity, input.Password);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();
            
            return new RegisterUserOutput
            {
                Message = "User created successfully!",
                IsSuccess = true,
            };
        }
        catch (Exception)
        {
            return new RegisterUserOutput
            {
                Message = "User did not create",
                IsSuccess = false
            };
        }
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
                new Claim(ClaimTypes.Email, input.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Username),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["AuthSettings:Key"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["AuthSettings:Issuer"],
                audience: _configuration["AuthSettings:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(30),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            var tokenAsString = new JwtSecurityTokenHandler().WriteToken(token);

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