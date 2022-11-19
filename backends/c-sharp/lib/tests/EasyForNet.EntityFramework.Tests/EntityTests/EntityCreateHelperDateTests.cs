using System;
using System.Threading.Tasks;
using Autofac;
using EasyForNet.EntityFramework.Helpers;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using EasyForNet.Exceptions.UserFriendly;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.EntityTests
{
    public class EntityCreateHelperDateTests : TestsBase
    {
        public EntityCreateHelperDateTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public async Task Create()
        {
            var dbContext = Scope.Resolve<EasyForNetEntityFrameworkTestsDb>();

            var entity = new SpecificHolidayEntity
            {
                Date = DateTime.Now
            };

            await EntityCreateHelper.AddAsync<EasyForNetEntityFrameworkTestsDb, SpecificHolidayEntity, long>(dbContext,
                entity, e => e.Date);

            await dbContext.SaveChangesAsync();

            dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

            var savedEntity = await dbContext.SpecificHolidays.FindAsync(entity.Id);

            Assert.NotNull(savedEntity);
            entity.Should().BeEquivalentTo(savedEntity);
        }

        [Fact]
        public async Task Create_Duplicate()
        {
            var dbContext = Scope.Resolve<EasyForNetEntityFrameworkTestsDb>();

            var entity = new SpecificHolidayEntity
            {
                Date = DateTime.Now.Date
            };
            await EntityCreateHelper.AddAsync<EasyForNetEntityFrameworkTestsDb, SpecificHolidayEntity, long>(dbContext,
                entity, e => e.Date);

            await dbContext.SaveChangesAsync();

            dbContext = NewScopeService<EasyForNetEntityFrameworkTestsDb>();

            var entityTwo = new SpecificHolidayEntity
            {
                Date = entity.Date
            };

            var exception = await Assert.ThrowsAsync<UniquePropertyException>(async () =>
                await EntityCreateHelper.AddAsync<EasyForNetEntityFrameworkTestsDb, SpecificHolidayEntity, long>(
                    dbContext, entityTwo, e => e.Date));

            Assert.Contains("Duplicate of Date not allowed", exception.Message);
        }
    }
}