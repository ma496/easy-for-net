using EasyForNet.Application.Account.Login;
using Microsoft.AspNetCore.Mvc;

namespace EasyForNet.Host.Controllers;

public class AccountController : ApiControllerBase
{
    [HttpPost]
    public async Task<LoginDto> Login(LoginCommand command)
    {
        return await Mediator.Send(command);
    }
}