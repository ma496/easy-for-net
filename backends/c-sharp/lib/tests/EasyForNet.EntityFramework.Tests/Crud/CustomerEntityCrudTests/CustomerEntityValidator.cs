using EasyForNet.EntityFramework.Tests.Data.Entities;
using FluentValidation;

namespace EasyForNet.EntityFramework.Tests.Crud.CustomerEntityCrudTests
{
    public class CustomerEntityValidator : AbstractValidator<CustomerEntity>
    {
        public CustomerEntityValidator()
        {
            RuleFor(c => c.CellNo)
                .NotEmpty();
        }
    }
}