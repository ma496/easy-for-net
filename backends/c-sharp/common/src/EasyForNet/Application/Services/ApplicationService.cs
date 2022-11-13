using AutoMapper;

namespace EasyForNet.Application.Services
{
    public abstract class ApplicationService : IApplicationService
    {
        protected IMapper Mapper { get; set; }
        protected ICurrentUser CurrentUser { get; set; }
    }
}