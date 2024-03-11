using FluentValidation;

namespace Efn.Features.Signup;

sealed public class Request
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public int Age { get; set; }
}

sealed class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(5);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MinimumLength(5);
        RuleFor(x => x.Username).NotEmpty().MinimumLength(5);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(5).MaximumLength(20);
    }
}

sealed class Response
{
    public string Message { get; set; }
}

