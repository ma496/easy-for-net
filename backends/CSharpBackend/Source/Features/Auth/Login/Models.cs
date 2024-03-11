using FluentValidation;

namespace Efn.Features.Auth.Login;

sealed class Request
{
    public string Username { get; set; }
    public string Password { get; set; }
}

sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Username).MinimumLength(5);
        RuleFor(x => x.Password).MinimumLength(5).MaximumLength(20);
    }
}
