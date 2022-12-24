// using Ardalis.GuardClauses;
// using EasyForNet.Domain.Services;
// using Microsoft.AspNetCore.Identity;
// using Microsoft.Extensions.Configuration;
// using Microsoft.IdentityModel.Tokens;
// using System;
// using System.IdentityModel.Tokens.Jwt;
// using System.Linq;
// using System.Security.Claims;
// using System.Text;
// using System.Threading.Tasks;
//
// namespace EasyForNet.EntityFramework.Identity;
//
// public class AuthManager : DomainService, IAuthManager
// {
//     private readonly UserManager<IdentityUser> _userManager;
//     private readonly IConfiguration _configuration;
//
//     public AuthManager(UserManager<IdentityUser> userManager, IConfiguration configuration)
//     {
//         _userManager = userManager;
//         _configuration = configuration;
//     }
//
//     public async Task<RegisterUserOutput> RegisterUserAsync(RegisterUserInput input)
//     {
//         Guard.Against.Null(input, nameof(input));
//
//         var identityUser = new IdentityUser
//         {
//             Email = input.Email,
//             UserName = input.Email,
//         };
//
//         var result = await _userManager.CreateAsync(identityUser, input.Password);
//
//         if (result.Succeeded)
//         {
//             return new RegisterUserOutput
//             {
//                 Message = "User created successfully!",
//                 IsSuccess = true,
//             };
//         }
//
//         return new RegisterUserOutput
//         {
//             Message = "User did not create",
//             IsSuccess = false,
//             Errors = result.Errors.Select(e => e.Description)
//         };
//     }
//
//     public async Task<LoginUserOutput> LoginUserAsync(LoginUserInput input)
//     {
//         Guard.Against.Null(input, nameof(input));
//
//         var user = await _userManager.FindByEmailAsync(input.Email);
//
//         if (user == null)
//         {
//             return new LoginUserOutput
//             {
//                 Message = "There is no user with that Email address",
//                 IsSuccess = false,
//             };
//         }
//
//         var result = await _userManager.CheckPasswordAsync(user, input.Password);
//
//         if (!result)
//             return new LoginUserOutput
//             {
//                 Message = "Invalid password",
//                 IsSuccess = false,
//             };
//
//         var claims = new[]
//         {
//             new Claim("Email", input.Email),
//             new Claim(ClaimTypes.NameIdentifier, user.Id),
//         };
//
//         var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["AuthSettings:Key"]));
//
//         var token = new JwtSecurityToken(
//             issuer: _configuration["AuthSettings:Issuer"],
//             audience: _configuration["AuthSettings:Audience"],
//             claims: claims,
//             expires: DateTime.Now.AddDays(30),
//             signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
//
//         var tokenAsString = new JwtSecurityTokenHandler().WriteToken(token);
//
//         return new LoginUserOutput
//         {
//             Message = "User login successfully!",
//             Token = tokenAsString,
//             IsSuccess = true,
//             ExpireDate = token.ValidTo
//         };
//     }
// }