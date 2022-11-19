using Bogus;
using EasyForNet.EntityFramework.Bogus;
using EasyForNet.EntityFramework.Tests.Data.Entities;

namespace EasyForNet.EntityFramework.Tests.GenerateData
{
    public class ProductGenerator : EfDataGenerator<ProductEntity>
    {
        private readonly ProductItemGenerator _productItemGenerator;

        public ProductGenerator(ProductItemGenerator productItemGenerator)
        {
            _productItemGenerator = productItemGenerator;
        }

        protected override Faker<ProductEntity> Faker()
        {
            return new Faker<ProductEntity>()
                .RuleFor(p => p.Model, f => $"{IncrementalId.Id}_{f.Commerce.Product()}")
                .RuleFor(p => p.Price, f => decimal.Parse(f.Commerce.Price()))
                .RuleFor(p => p.Items, f => _productItemGenerator.Generate(f.Random.Number(1, 10)));
        }
    }
}