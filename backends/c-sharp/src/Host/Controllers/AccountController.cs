using EasyForNet.Application.Account.SignIn;
using Microsoft.AspNetCore.Mvc;

namespace EasyForNet.Host.Controllers;

public class AccountController : ApiControllerBase
{
    [HttpPost("SignIn")]
    public async Task<SignInDto> SignIn(SignInCommand command)
    {
        return await Mediator.Send(command);
    }
}