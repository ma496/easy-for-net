using CSharpTemplate.Common.Identity.Dto;

namespace CSharpTemplate.Common.Identity;

public interface IAuthManager
{
    Task RegisterUserAsync(RegisterUserInput input);
    Task<LoginUserOutput> LoginUserAsync(LoginUserInput input);
}