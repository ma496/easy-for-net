using AutoMapper;
using EasyForNet.EntityFramework.Tests.Data.Entities;

namespace EasyForNet.EntityFramework.Tests.Crud.CustomerEntityCrudTests
{
    public class CustomerEntityCrudTestsProfile : Profile
    {
        public CustomerEntityCrudTestsProfile()
        {
            CreateMap<CustomerDto, CustomerEntity>(MemberList.Source);
            CreateMap<CustomerEntity, CustomerDto>();
        }
    }
}