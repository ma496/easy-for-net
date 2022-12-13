using Autofac;
using AutoMapper;
using EasyForNet.Application.Dto.Crud;
using EasyForNet.Application.Dto.Entities.Audit;
using EasyForNet.EntityFramework.Crud;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using EasyForNet.Repository;
using EasyForNet.Tests.Share.Extensions;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Crud;

public class CrudAppServiceTests : TestsBase
{
    public CrudAppServiceTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task Create_TestAsync()
    {
        var customerAppService = Scope.Resolve<CustomerAppService>();
        var customer = NewCustomer();
        var returnCustomer = await customerAppService.CreateAsync(customer);
            
        returnCustomer.Id.Should().BeGreaterThan(0);

        var savedCustomer = await customerAppService.GetAsync(returnCustomer.Id);

        savedCustomer.Should().NotBeNull();
        savedCustomer.Should().BeEquivalentTo(returnCustomer);
    }

    [Fact]
    public async Task Update_TestAsync()
    {
        var customerAppService = Scope.Resolve<CustomerAppService>();
        var customer = NewCustomer();
        var returnCustomer = await customerAppService.CreateAsync(customer);

        returnCustomer.Id.Should().BeGreaterThan(0);

        customerAppService = NewScopeService<CustomerAppService>();
        returnCustomer.Name = $"Name_{IncrementalId.Id}";
        returnCustomer.Code = IncrementalId.Id;
        await customerAppService.UpdateAsync(returnCustomer.Id, returnCustomer);

        var savedCustomer = await customerAppService.GetAsync(returnCustomer.Id);

        savedCustomer.Should().NotBeNull();
        savedCustomer.Should().BeEquivalentTo(returnCustomer, opt => opt.BeCloseTo(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Delete_TestAsync()
    {
        var customerAppService = Scope.Resolve<CustomerAppService>();
        var customer = NewCustomer();
        var returnCustomer = await customerAppService.CreateAsync(customer);

        returnCustomer.Id.Should().BeGreaterThan(0);

        var savedCustomer = await customerAppService.GetAsync(returnCustomer.Id);

        savedCustomer.Should().NotBeNull();
        savedCustomer.Should().BeEquivalentTo(returnCustomer);

        customerAppService = NewScopeService<CustomerAppService>();

        await customerAppService.DeleteAsync(returnCustomer.Id);

        savedCustomer = await customerAppService.GetAsync(returnCustomer.Id);

        savedCustomer.Should().BeNull();
    }

    [Fact]
    public async Task UndoDelete_TestAsync()
    {
        var customerAppService = Scope.Resolve<CustomerAppService>();
        var customer = NewCustomer();
        var returnCustomer = await customerAppService.CreateAsync(customer);

        returnCustomer.Id.Should().BeGreaterThan(0);

        var savedCustomer = await customerAppService.GetAsync(returnCustomer.Id);

        savedCustomer.Should().NotBeNull();
        savedCustomer.Should().BeEquivalentTo(returnCustomer);

        customerAppService = NewScopeService<CustomerAppService>();

        await customerAppService.DeleteAsync(returnCustomer.Id);

        savedCustomer = await customerAppService.GetAsync(returnCustomer.Id);

        savedCustomer.Should().BeNull();

        customerAppService = NewScopeService<CustomerAppService>();

        await customerAppService.UndoDeleteAsync(returnCustomer.Id);

        savedCustomer = await customerAppService.GetAsync(returnCustomer.Id);

        savedCustomer.Should().NotBeNull();
    }

    [Fact]
    public async Task List_TestAsync()
    {
        var customerAppService = Scope.Resolve<CustomerAppService>();
        var customers = NewCustomers(10);
        customers.ForEach(async c => await customerAppService.CreateAsync(c));

        var savedCustomers = await customerAppService.GetListAsync( new PagedAndSortedResultRequest
        {
            MaxResultCount = 4,
            SkipCount = 2
        });

        savedCustomers.Should().NotBeNull();
        savedCustomers.TotalCount.Should().BeGreaterThanOrEqualTo(10);
        savedCustomers.Items.Count.Should().Be(4);
    }

    #region Helpers

    private CustomerDto NewCustomer()
    {
        int id = IncrementalId.Id;
        return new CustomerDto
        {
            Name = $"Name_{id}",
            Code = id,
            CellNo = $"5643432_{id}",
            IdCard = $"6336_4353_3455_42{id}"
        };
    }

    private List<CustomerDto> NewCustomers(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => NewCustomer()).ToList();
    }

    #endregion

    #region Classes

    [AutoMap(typeof(CustomerEntity), ReverseMap = true)]
    public class CustomerDto : SoftDeleteAuditEntityDto<long>
    {
        public long Code { get; set; }

        [Required] public string Name { get; set; }

        public string IdCard { get; set; }

        public string CellNo { get; set; }
    }

    public class CustomerAppService : CrudAppService<CustomerEntity, long, PagedAndSortedResultRequest,
        CustomerDto>
    {
        public CustomerAppService(IRepository<CustomerEntity, long> repository) : base(repository)
        {
        }
    }

    #endregion
}