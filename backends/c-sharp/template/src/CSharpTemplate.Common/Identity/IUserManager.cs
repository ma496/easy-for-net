using CSharpTemplate.Common.Identity.Dto;
using CSharpTemplate.Common.Identity.Entities;

namespace CSharpTemplate.Common.Identity;

public interface IUserManager
{
    Task<AppUser> CreateAsync(UserDto user);
    Task UpdatePasswordAsync(AppUser user, string password);
    Task<UserDto?> GetByIdAsync(long id);
    Task<UserDto?> GetByEmailAsync(string email);
}