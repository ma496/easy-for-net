using FluentValidation;

namespace EasyForNet.EntityFramework.Tests.Crud.CustomerEntityCrudTests;

public class CustomerDtoValidator : AbstractValidator<CustomerDto>
{
    public CustomerDtoValidator()
    {
        RuleFor(c => c.IdCard)
            .NotEmpty();
    }
}