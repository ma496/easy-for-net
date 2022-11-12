using Bogus;
using EasyForNet.Application.Dependencies;
using EasyForNet.EntityFramework.Bogus;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.Data.Entities;

namespace EasyForNet.EntityFramework.Tests.GenerateData
{
    public class ProductItemGenerator : DataGenerator<EasyForNetEntityFrameworkTestsDb, ProductItemEntity>,
        IScopedDependency
    {
        public ProductItemGenerator(EasyForNetEntityFrameworkTestsDb dbContext) : base(dbContext)
        {
        }

        protected override Faker<ProductItemEntity> Faker()
        {
            return new Faker<ProductItemEntity>()
                .RuleFor(pi => pi.SerialNo, f => $"{IncrementalId.Id}_{f.Commerce.Ean13()}");
        }
    }
}