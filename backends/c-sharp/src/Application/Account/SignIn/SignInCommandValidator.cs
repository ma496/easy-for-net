using FluentValidation;

namespace EasyForNet.Application.Account.SignIn;

public class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty();
        RuleFor(x => x.Password)
            .NotEmpty();
    }
}