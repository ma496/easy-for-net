using EasyForNet.Application.Common.Interfaces;
using MediatR;

namespace EasyForNet.Application.Account.Login;

public class LoginCommand : IRequest<LoginDto>
{
    public string Username { get; set; }
    public string Password { get; set; }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginDto>
{
    private readonly IIdentityService _identityService;

    public LoginCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }
    
    public async Task<LoginDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.LoginAsync(request.Username, request.Password);
        return new LoginDto { IsSuccess = result.isSuccess, Token = result.token };
    }
}