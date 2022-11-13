using AutoMapper;

namespace EasyForNet.Domain.Services
{
    public abstract class DomainService : IDomainService
    {
        protected IMapper Mapper { get; set; }
        protected ICurrentUser CurrentUser { get; set; }
    }
}