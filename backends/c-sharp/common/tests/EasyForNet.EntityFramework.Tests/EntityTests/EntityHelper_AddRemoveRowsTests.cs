using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using EasyForNet.EntityFramework.Helpers;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using EasyForNet.EntityFramework.Tests.GenerateData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.EntityTests
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class EntityHelper_AddRemoveRowsTests : TestsBase
    {
        public EntityHelper_AddRemoveRowsTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public async Task AddRemoveRowsTest()
        {
            var dbContext = Scope.Resolve<EasyForNetEntityFrameworkTestsDb>();
            var productGenerator = Scope.Resolve<ProductGenerator>();
            var productItemGenerator = Scope.Resolve<ProductItemGenerator>();

            var product = productGenerator.Generate();
            product.Items = productItemGenerator.Generate(10);

            await EntityCreateHelper.AddAsync<EasyForNetEntityFrameworkTestsDb, ProductEntity, long>(dbContext, product,
                p => p.Model);

            await dbContext.SaveChangesAsync();

            dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

            var savedProduct =
                await dbContext.Products.Include(p => p.Items).SingleOrDefaultAsync(p => p.Id == product.Id);
            var productItems = productItemGenerator.Generate(3).Select(pi =>
            {
                pi.ProductId = product.Id;
                return pi;
            }).ToList().Concat(savedProduct.Items.Take(5)).ToList();

            await EntityHelper.AddRemoveRowsAsync(dbContext.ProductItems, savedProduct.Items, productItems,
                (e, e1) => e.Id == e1.Id,
                pi => new ProductItemEntity {SerialNo = pi.SerialNo, ProductId = pi.ProductId});

            await dbContext.SaveChangesAsync();

            var updateSavedProduct =
                await dbContext.Products.Include(p => p.Items).SingleAsync(p => p.Id == product.Id);

            Assert.NotNull(updateSavedProduct);
            Assert.NotNull(updateSavedProduct.Items);
            Assert.Equal(8, savedProduct.Items.Count);
        }
    }
}