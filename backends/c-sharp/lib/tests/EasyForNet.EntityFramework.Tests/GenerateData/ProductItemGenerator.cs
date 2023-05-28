using Bogus;
using EasyForNet.EntityFramework.Bogus;
using EasyForNet.EntityFramework.Tests.Data.Entities;

namespace EasyForNet.EntityFramework.Tests.GenerateData;

public class ProductItemGenerator : EfDataGenerator<ProductItemEntity>
{
    protected override Faker<ProductItemEntity> Faker()
    {
        return new Faker<ProductItemEntity>()
            .RuleFor(pi => pi.SerialNo, f => $"{IncrementalId.Id}_{f.Commerce.Ean13()}");
    }
}