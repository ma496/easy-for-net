using Autofac;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using EasyForNet.EntityFramework.Tests.GenerateData;
using EasyForNet.Exceptions.UserFriendly;
using EasyForNet.Extensions;
using EasyForNet.Repository;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Repository;

public class EfRepositoryTests : TestsBase
{
    private CustomerGenerator _customerGenerator;

    public EfRepositoryTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        _customerGenerator = Scope.Resolve<CustomerGenerator>();
    }

    [Fact]
    public void Should_Create()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customer = _customerGenerator.Generate();
        repository.Create(customer, true);

        var savedCustomer = repository.Find(customer.Id);

        savedCustomer.Should().NotBeNull();
        savedCustomer.Name.Should().Be(customer.Name);
    }

    [Fact]
    public async Task Should_CreateAsync()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customer = _customerGenerator.Generate();
        await repository.CreateAsync(customer, true);

        var savedCustomer = await repository.FindAsync(customer.Id);

        savedCustomer.Should().NotBeNull();
        savedCustomer.Name.Should().Be(customer.Name);
    }

    [Fact]
    public void Should_CreateRange()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customers = _customerGenerator.Generate(10);
        repository.CreateRange(customers, true);

        var ids = customers.Select(x => x.Id).ToList();
        var savedCustomers = repository.GetAll().Where(x => ids.Contains(x.Id)).ToList();

        savedCustomers.Should().NotBeNull();
        savedCustomers.Count.Should().Be(customers.Count);
        for (int i = 0; i < savedCustomers.Count; i++)
        {
            var customer = customers[i];
            var savedCustomer = savedCustomers[i];
            savedCustomer.Name.Should().Be(customer.Name);
        }
    }

    [Fact]
    public async Task Should_CreateRangeAsync()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customers = _customerGenerator.Generate(10);
        await repository.CreateRangeAsync(customers, true);

        var ids = customers.Select(x => x.Id).ToList();
        var savedCustomers = await repository.GetAll().Where(x => ids.Contains(x.Id)).ToListAsync();

        savedCustomers.Should().NotBeNull();
        savedCustomers.Count.Should().Be(customers.Count);
        for (int i = 0; i < savedCustomers.Count; i++)
        {
            var customer = customers[i];
            var savedCustomer = savedCustomers[i];
            savedCustomer.Name.Should().Be(customer.Name);
        }
    }

    [Fact]
    public void Should_Update()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customer = _customerGenerator.Generate();
        repository.Create(customer, true);

        var savedCustomer = repository.Find(customer.Id);

        savedCustomer.Should().NotBeNull();
        savedCustomer.Name.Should().Be(customer.Name);

        repository = NewScopeService<IRepository<CustomerEntity, long>>();
        savedCustomer.Name = $"Name_{IncrementalId.Id}";
        repository.Update(savedCustomer, true);

        var updatedCustomer = repository.Find(customer.Id);

        updatedCustomer.Should().NotBeNull();
        updatedCustomer.Name.Should().Be(savedCustomer.Name);
    }

    [Fact]
    public async Task Should_UpdateAsync()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customer = _customerGenerator.Generate();
        await repository.CreateAsync(customer, true);

        var savedCustomer = await repository.FindAsync(customer.Id);

        savedCustomer.Should().NotBeNull();
        savedCustomer.Name.Should().Be(customer.Name);

        repository = NewScopeService<IRepository<CustomerEntity, long>>();
        savedCustomer.Name = $"Name_{IncrementalId.Id}";
        await repository.UpdateAsync(savedCustomer, true);

        var updatedCustomer = await repository.FindAsync(customer.Id);

        updatedCustomer.Should().NotBeNull();
        updatedCustomer.Name.Should().Be(savedCustomer.Name);
    }

    [Fact]
    public void Should_UpdateRange()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customers = _customerGenerator.Generate(10);
        repository.CreateRange(customers, true);

        var customersForUpdate = customers.Clone();
        customersForUpdate.ForEach(c => 
        {
            c.Name = $"Name_{IncrementalId.Id}";
        });
        repository = NewScopeService<IRepository<CustomerEntity, long>>();
        repository.UpdateRange(customersForUpdate, true);

        var ids = customers.Select(x => x.Id).ToList();
        var savedCustomers = repository.GetAll().Where(x => ids.Contains(x.Id)).ToList();

        savedCustomers.Should().NotBeNull();
        savedCustomers.Count.Should().Be(customers.Count);
        for (int i = 0; i < savedCustomers.Count; i++)
        {
            var customer = customers[i];
            var customerForUpdate = customersForUpdate[i];
            var savedCustomer = savedCustomers[i];
            savedCustomer.Name.Should().NotBe(customer.Name);
            savedCustomer.Name.Should().Be(customerForUpdate.Name);
        }
    }

    [Fact]
    public async Task Should_UpdateRangeAsync()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customers = _customerGenerator.Generate(10);
        await repository.CreateRangeAsync(customers, true);

        var customersForUpdate = customers.Clone();
        customersForUpdate.ForEach(c =>
        {
            c.Name = $"Name_{IncrementalId.Id}";
        });
        repository = NewScopeService<IRepository<CustomerEntity, long>>();
        await repository.UpdateRangeAsync(customersForUpdate, true);

        var ids = customers.Select(x => x.Id).ToList();
        var savedCustomers = await repository.GetAll().Where(x => ids.Contains(x.Id)).ToListAsync();

        savedCustomers.Should().NotBeNull();
        savedCustomers.Count.Should().Be(customers.Count);
        for (int i = 0; i < savedCustomers.Count; i++)
        {
            var customer = customers[i];
            var customerForUpdate = customersForUpdate[i];
            var savedCustomer = savedCustomers[i];
            savedCustomer.Name.Should().NotBe(customer.Name);
            savedCustomer.Name.Should().Be(customerForUpdate.Name);
        }
    }

    [Fact]
    public void Should_Delete()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customer = _customerGenerator.Generate();
        repository.Create(customer, true);

        repository = NewScopeService<IRepository<CustomerEntity, long>>();
        repository.Delete(customer.Id, true);

        var savedCustomer = repository.Find(customer.Id);

        savedCustomer.Should().BeNull();
    }

    [Fact]
    public async Task Should_DeleteAsync()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customer = _customerGenerator.Generate();
        await repository.CreateAsync(customer, true);

        repository = NewScopeService<IRepository<CustomerEntity, long>>();
        await repository.DeleteAsync(customer.Id, true);

        var savedCustomer = await repository.FindAsync(customer.Id);

        savedCustomer.Should().BeNull();
    }

    [Fact]
    public void Should_DeleteRange()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customers = _customerGenerator.Generate(10);
        repository.CreateRange(customers, true);

        var keys = customers.Select(c => c.Id).ToList();
        repository = NewScopeService<IRepository<CustomerEntity, long>>();

        repository.DeleteRange(keys, true);

        var savedCustomers = repository.GetAll().Where(e => keys.Contains(e.Id)).ToList();

        savedCustomers.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Should_DeleteRangeAsync()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customers = _customerGenerator.Generate(10);
        await repository.CreateRangeAsync(customers, true);

        var keys = customers.Select(c => c.Id).ToList();
        repository = NewScopeService<IRepository<CustomerEntity, long>>();

        await repository.DeleteRangeAsync(keys, true);

        var savedCustomers = await repository.GetAll().Where(e => keys.Contains(e.Id)).ToListAsync();

        savedCustomers.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Should_UndoDelete()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customer = _customerGenerator.Generate();
        repository.Create(customer, true);

        repository = NewScopeService<IRepository<CustomerEntity, long>>();
        repository.Delete(customer.Id, true);

        var savedCustomer = repository.Find(customer.Id);

        savedCustomer.Should().BeNull();

        repository = NewScopeService<IRepository<CustomerEntity, long>>();

        repository.UndoDelete(customer.Id, true);

        savedCustomer = repository.Find(customer.Id);

        savedCustomer.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_UndoDeleteAsync()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customer = _customerGenerator.Generate();
        await repository.CreateAsync(customer, true);

        repository = NewScopeService<IRepository<CustomerEntity, long>>();
        await repository.DeleteAsync(customer.Id, true);

        var savedCustomer = await repository.FindAsync(customer.Id);

        savedCustomer.Should().BeNull();

        repository = NewScopeService<IRepository<CustomerEntity, long>>();

        await repository.UndoDeleteAsync(customer.Id, true);

        savedCustomer = await repository.FindAsync(customer.Id);

        savedCustomer.Should().NotBeNull();
    }

    [Fact]
    public void Should_UndoDeleteRange()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customers = _customerGenerator.Generate(10);
        repository.CreateRange(customers, true);

        var keys = customers.Select(c => c.Id).ToList();
        repository = NewScopeService<IRepository<CustomerEntity, long>>();

        repository.DeleteRange(keys, true);

        var savedCustomers = repository.GetAll().Where(e => keys.Contains(e.Id)).ToList();

        savedCustomers.Should().BeNullOrEmpty();

        repository = NewScopeService<IRepository<CustomerEntity, long>>();

        repository.UndoDeleteRange(keys, true);

        savedCustomers = repository.GetAll().Where(e => keys.Contains(e.Id)).ToList();

        savedCustomers.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_UndoDeleteRangeAsync()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customers = _customerGenerator.Generate(10);
        await repository.CreateRangeAsync(customers, true);

        var keys = customers.Select(c => c.Id).ToList();
        repository = NewScopeService<IRepository<CustomerEntity, long>>();

        await repository.DeleteRangeAsync(keys, true);

        var savedCustomers = await repository.GetAll().Where(e => keys.Contains(e.Id)).ToListAsync();

        savedCustomers.Should().BeNullOrEmpty();

        repository = NewScopeService<IRepository<CustomerEntity, long>>();

        await repository.UndoDeleteRangeAsync(keys, true);

        savedCustomers = await repository.GetAll().Where(e => keys.Contains(e.Id)).ToListAsync();

        savedCustomers.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Should_GetById()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customer = _customerGenerator.Generate();
        repository.Create(customer, true);

        var savedCustomer = repository.GetById(customer.Id);

        savedCustomer.Should().NotBeNull();
        savedCustomer.Name.Should().Be(customer.Name);

        repository = NewScopeService<IRepository<CustomerEntity, long>>();
        repository.Delete(savedCustomer.Id, true);

        Assert.Throws<EntityNotFoundException>(() => repository.GetById(savedCustomer.Id));
    }

    [Fact]
    public async Task Should_GetByIdAsync()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customer = _customerGenerator.Generate();
        await repository.CreateAsync(customer, true);

        var savedCustomer = await repository.GetByIdAsync(customer.Id);

        savedCustomer.Should().NotBeNull();
        savedCustomer.Name.Should().Be(customer.Name);

        repository = NewScopeService<IRepository<CustomerEntity, long>>();
        await repository.DeleteAsync(savedCustomer.Id, true);

        await Assert.ThrowsAsync<EntityNotFoundException>(async () => await repository.GetByIdAsync(savedCustomer.Id));
    }

    [Fact]
    public void Should_SaveChanges()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customer = _customerGenerator.Generate();
        repository.Create(customer);

        var savedCustomer = repository.Find(customer.Id);

        savedCustomer.Should().BeNull();

        repository.SaveChanges();

        savedCustomer = repository.Find(customer.Id);

        savedCustomer.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_SaveChangesAsync()
    {
        var repository = Scope.Resolve<IRepository<CustomerEntity, long>>();
        var customer = _customerGenerator.Generate();
        await repository.CreateAsync(customer);

        var savedCustomer = await repository.FindAsync(customer.Id);

        savedCustomer.Should().BeNull();

        await repository.SaveChangesAsync();

        savedCustomer = await repository.FindAsync(customer.Id);

        savedCustomer.Should().NotBeNull();
    }
}