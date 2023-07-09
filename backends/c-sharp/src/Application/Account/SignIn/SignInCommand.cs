﻿using EasyForNet.Application.Common.Interfaces;
using MediatR;

namespace EasyForNet.Application.Account.SignIn;

public class SignInCommand : IRequest<SignInDto>
{
    public string Username { get; set; }
    public string Password { get; set; }
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
        var result = await _identityService.SignInAsync(request.Username, request.Password);
        return new SignInDto { IsSuccess = result.isSuccess, Token = result.token };
    }
}