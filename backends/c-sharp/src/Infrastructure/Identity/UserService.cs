using EasyForNet.Application.Identity;
using EasyForNet.Application.Identity.Dto;
using EasyForNet.Domain.Exceptions;
using EasyForNet.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace EasyForNet.Infrastructure.Identity;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<string> CreateUserAsync(UserCreateDto input)
    {
        var user = ApplicationUser.Create(input.UserName, input.Email, input.FirstName, input.LastName);

        var result = await _userManager.CreateAsync(user, input.Password);

        if (!result.Succeeded)
        {
            throw new AppException(string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));
        }   

        return user.Id;
    }

    public async Task UpdateUserAsync(string id, UserUpdateDto input)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
        {
            throw new AppException("User not found");
        }

        user.Update(input.Email, input.FirstName, input.LastName);

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new AppException(string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));
        }
    }

    public async Task DeleteUserAsync(string userId)
    {
        var user = _userManager.Users.SingleOrDefault(u => u.Id == userId);

        if (user != null)
        {
            await _userManager.DeleteAsync(user);
        }
    }
}
