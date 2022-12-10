using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using EasyForNet.Application.Dependencies;
using EasyForNet.Application.Dto.Entities;
using EasyForNet.EntityFramework.Crud;
using EasyForNet.EntityFramework.Helpers;
using EasyForNet.EntityFramework.Tests.Base;
using EasyForNet.EntityFramework.Tests.Data;
using EasyForNet.EntityFramework.Tests.Data.Entities;
using Xunit;
using Xunit.Abstractions;

namespace EasyForNet.EntityFramework.Tests.Crud;

public class SpecificHolidayEntityCrudTests : CrudTestsBase<SpecificHolidayCrudActions, SpecificHolidayEntity,
    long, SpecificHolidayListDto, SpecificHolidayCreateDto, SpecificHolidayCreateDto, SpecificHolidayUpdateDto,
    SpecificHolidayUpdateDto, SpecificHolidayGetDto>
{
    public SpecificHolidayEntityCrudTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
    }

    [Fact]
    public async Task ListTest()
    {
        await InternalListTestAsync();
    }

    [Fact]
    public async Task CreateTest()
    {
        await InternalCreateTestAsync();
    }

    [Fact]
    public async Task CreateDuplicateTest()
    {
        await InternalCreateDuplicateTest(1, 1, _ => nameof(SpecificHolidayCreateDto.Date));
    }

    [Fact]
    public async Task UpdateTest()
    {
        await InternalUpdateTestAsync(dto => dto.Date = DateTime.Now.AddMinutes(30));
    }

    [Fact]
    public async Task UpdateDuplicateTest()
    {
        await InternalUpdateDuplicateTest(2, 1, (dto, dtos) =>
        {
            dto.Date = dtos[1].Date;
            return nameof(SpecificHolidayUpdateDto.Date);
        });
    }

    [Fact]
    public async Task DeleteTest()
    {
        await InternalDeleteTestAsync();
    }

    [Fact]
    public async Task GetTest()
    {
        await InternalGetTestAsync();
    }

    protected override SpecificHolidayCreateDto NewDto()
    {
        Thread.Sleep(10);

        return new()
        {
            Date = DateTime.Now
        };
    }
}

public class SpecificHolidayListDto : EntityDto<long>
{
    public DateTime Date { get; set; }
}

public class SpecificHolidayCreateDto : EntityDto<long>
{
    public DateTime Date { get; set; }
}

public class SpecificHolidayUpdateDto : EntityDto<long>
{
    public DateTime Date { get; set; }
}

public class SpecificHolidayGetDto : EntityDto<long>
{
    public DateTime Date { get; set; }
}

public class SpecificHolidayProfile : Profile
{
    public SpecificHolidayProfile()
    {
        CreateMap<SpecificHolidayEntity, SpecificHolidayListDto>();
        CreateMap<SpecificHolidayCreateDto, SpecificHolidayEntity>(MemberList.Source);
        CreateMap<SpecificHolidayUpdateDto, SpecificHolidayEntity>(MemberList.Source);
        CreateMap<SpecificHolidayEntity, SpecificHolidayUpdateDto>();
        CreateMap<SpecificHolidayEntity, SpecificHolidayGetDto>();

        // Mapping for tests
        CreateMap<SpecificHolidayGetDto, SpecificHolidayUpdateDto>();
        CreateMap<SpecificHolidayCreateDto, SpecificHolidayUpdateDto>();
    }
}

public class SpecificHolidayCrudActions : CrudActions<EasyForNetEntityFrameworkTestsDb, SpecificHolidayEntity, long,
        SpecificHolidayListDto, SpecificHolidayCreateDto, SpecificHolidayCreateDto, SpecificHolidayUpdateDto,
        SpecificHolidayUpdateDto, SpecificHolidayGetDto>,
    IScopedDependency
{
    public SpecificHolidayCrudActions(EasyForNetEntityFrameworkTestsDb dbContext, IMapper mapper) : base(dbContext,
        mapper, true)
    {
    }

    protected override List<UniqueProperty> UniqueProperties()
    {
        return new()
        {
            new UniqueProperty(nameof(SpecificHolidayListDto.Date))
        };
    }
}