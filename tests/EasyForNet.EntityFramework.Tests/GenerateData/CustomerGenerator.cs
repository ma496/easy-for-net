using Bogus;
using EasyForNet.Application.Dependencies;
using EasyForNet.EntityFramework.Bogus;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.Data.Entities;

namespace EasyForNet.EntityFramework.Tests.GenerateData
{
    public class CustomerGenerator : DataGenerator<EasyForNetEntityFrameworkTestsDb, CustomerEntity>, IScopedDependency
    {
        public CustomerGenerator(EasyForNetEntityFrameworkTestsDb dbContext) : base(dbContext)
        {
        }

        protected override Faker<CustomerEntity> Faker()
        {
            return new Faker<CustomerEntity>()
                .RuleFor(c => c.Code, f => IncrementalId.Id)
                .RuleFor(c => c.IdCard, f => $"{IncrementalId.Id}434-4342-5454-9534")
                .RuleFor(c => c.Name, f => f.Name.FullName())
                .RuleFor(c => c.CellNo, f => f.Phone.PhoneNumber());
        }
    }
}