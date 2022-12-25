namespace CSharpTemplate.Common.Identity;

public interface IAuthManager
{
    Task<RegisterUserOutput> RegisterUserAsync(RegisterUserInput input);
    Task<LoginUserOutput> LoginUserAsync(LoginUserInput input);
}