using System;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Application.Services
{
    public abstract class ApplicationService : IApplicationService
    {
        protected IServiceProvider ServiceProvider { get; }
        protected IMapper Mapper { get; }
        protected ICurrentUser CurrentUser { get; }
        
        protected ApplicationService(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Mapper = serviceProvider.GetRequiredService<IMapper>();
            CurrentUser = serviceProvider.GetRequiredService<ICurrentUser>();
        }
    }
}