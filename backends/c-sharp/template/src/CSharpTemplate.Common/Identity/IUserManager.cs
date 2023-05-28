using CSharpTemplate.Common.Identity.Dto;
using CSharpTemplate.Common.Identity.Entities;

namespace CSharpTemplate.Common.Identity;

public interface IUserManager
{
    Task<User> CreateAsync(UserDto user);
    Task UpdatePasswordAsync(User user, string password);
    Task<UserDto?> GetByIdAsync(long id);
    Task<UserDto?> GetByEmailAsync(string email);
    Task<List<UserDto>> GetListAsync();
}