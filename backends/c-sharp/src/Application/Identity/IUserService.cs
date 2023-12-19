using EasyForNet.Application.Identity.Dto;

namespace EasyForNet.Application.Identity;

public interface IUserService
{
    Task<string> CreateUserAsync(UserCreateDto input);

    Task UpdateUserAsync(string id, UserUpdateDto input);

    Task DeleteUserAsync(string userId);
}
