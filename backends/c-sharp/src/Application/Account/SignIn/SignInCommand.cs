using EasyForNet.Application.Common.Interfaces;
using MediatR;

namespace EasyForNet.Application.Account.SignIn;

public class SignInCommand : IRequest<SignInDto>
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class SignInCommandHandler : IRequestHandler<SignInCommand, SignInDto>
{
    private readonly IIdentityService _identityService;

    public SignInCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<SignInDto> Handle(SignInCommand request, CancellationToken cancellationToken)
    {
        var token = await _identityService.SignInAsync(request.Username, request.Password);
        return new SignInDto { Token = token };
    }
}