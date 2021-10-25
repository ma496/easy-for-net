using System;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Domain.Services
{
    public abstract class DomainService : IDomainService
    {
        protected IServiceProvider ServiceProvider { get; }
        protected IMapper Mapper { get; }
        protected ICurrentUser CurrentUser { get; }
        
        protected DomainService(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Mapper = serviceProvider.GetRequiredService<IMapper>();
            CurrentUser = serviceProvider.GetRequiredService<ICurrentUser>();
        }
    }
}