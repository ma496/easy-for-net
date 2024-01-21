using EasyForNet.Application.Account.SignIn;
using EasyForNet.Application.Common.Security;
using Microsoft.AspNetCore.Mvc;

namespace EasyForNet.Host.Controllers;

[Authorize(Roles = "Admin")]
public class AccountController : ApiControllerBase
{
    [HttpPost("SignIn")]
    public async Task<SignInDto> SignIn(SignInCommand command)
    {
        return await Mediator.Send(command);
    }
}