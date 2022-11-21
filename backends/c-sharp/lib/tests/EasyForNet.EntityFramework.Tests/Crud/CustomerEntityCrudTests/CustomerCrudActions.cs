using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using EasyForNet.Application.Dependencies;
using EasyForNet.EntityFramework.Crud;
using EasyForNet.EntityFramework.Helpers;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using FluentValidation;

namespace EasyForNet.EntityFramework.Tests.Crud.CustomerEntityCrudTests;

public class CustomerCrudActions : CrudActions<EasyForNetEntityFrameworkTestsDb, CustomerEntity, long, CustomerDto,
        CustomerDto, CustomerDto, CustomerDto, CustomerDto, CustomerDto>,
    IScopedDependency
{
    public bool IsBeforeCreate { get; private set; }
    public bool IsAfterCreate { get; private set; }
    public bool IsBeforeUpdate { get; private set; }
    public bool IsAfterUpdate { get; private set; }
    public bool IsBeforeDelete { get; private set; }
    public bool IsAfterDelete { get; private set; }
    public bool IsBeforeUndoDelete { get; private set; }
    public bool IsAfterUndoDelete { get; private set; }
    public bool IsBeforeList { get; private set; }
    public bool IsBeforeGet { get; private set; }

    public CustomerCrudActions(EasyForNetEntityFrameworkTestsDb dbContext, IMapper mapper) : base(dbContext, mapper,
        true)
    {
    }

    protected override AbstractValidator<CustomerEntity> EntityValidator()
    {
        return new CustomerEntityValidator();
    }

    protected override AbstractValidator<CustomerDto> CreateDtoValidator()
    {
        return new CustomerDtoValidator();
    }

    protected override AbstractValidator<CustomerDto> UpdateDtoValidator()
    {
        return new CustomerDtoValidator();
    }

    protected override async Task BeforeCreateAsync(CustomerDto dto, CustomerEntity entity)
    {
        IsBeforeCreate = true;
        await base.BeforeCreateAsync(dto, entity);
    }

    protected override async Task AfterCreateAsync(CustomerDto dto, CustomerEntity entity)
    {
        IsAfterCreate = true;
        await base.AfterCreateAsync(dto, entity);
    }

    protected override async Task BeforeUpdateAsync(CustomerDto dto, CustomerEntity entity)
    {
        IsBeforeUpdate = true;
        await base.BeforeUpdateAsync(dto, entity);
    }

    protected override async Task AfterUpdateAsync(CustomerDto dto, CustomerEntity entity)
    {
        IsAfterUpdate = true;
        await base.AfterUpdateAsync(dto, entity);
    }

    protected override async Task BeforeDeleteAsync(long id)
    {
        IsBeforeDelete = true;
        await base.BeforeDeleteAsync(id);
    }

    protected override async Task AfterDeleteAsync(long id)
    {
        IsAfterDelete = true;
        await base.AfterDeleteAsync(id);
    }

    protected override async Task BeforeUndoDeleteAsync(long id)
    {
        IsBeforeUndoDelete = true;
        await base.BeforeUndoDeleteAsync(id);
    }

    protected override async Task AfterUndoDeleteAsync(long id)
    {
        IsAfterUndoDelete = true;
        await base.AfterUndoDeleteAsync(id);
    }

    protected override async Task<IQueryable<CustomerEntity>> BeforeListAsync(IQueryable<CustomerEntity> query)
    {
        IsBeforeList = true;
        return await base.BeforeListAsync(query);
    }

    protected override async Task<IQueryable<CustomerEntity>> BeforeGetAsync(IQueryable<CustomerEntity> query)
    {
        IsBeforeGet = true;
        return await base.BeforeGetAsync(query);
    }

    protected override List<UniqueProperty> UniqueProperties()
    {
        return new()
        {
            new UniqueProperty(nameof(CustomerEntity.Code)),
            new UniqueProperty(nameof(CustomerEntity.IdCard))
        };
    }
}