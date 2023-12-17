using EasyForNet.Application.Common.Models;

namespace EasyForNet.Application.Identity;

public interface IUserService
{
    Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password);

    Task<Result> DeleteUserAsync(string userId);
}
