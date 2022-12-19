using System.Threading.Tasks;

namespace EasyForNet.EntityFramework.Identity;

public interface IAuthManager
{
    Task<RegisterUserOutput> RegisterUserAsync(RegisterUserInput input);
    Task<LoginUserOutput> LoginUserAsync(LoginUserInput input);
}