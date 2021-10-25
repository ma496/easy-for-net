using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Events.Local
{
    public abstract class LocalEventHandler<TEvent> : ILocalEventHandler<TEvent>
        where TEvent : class
    {
        protected IServiceProvider ServiceProvider { get; }
        protected IMapper Mapper { get; }
        protected ICurrentUser CurrentUser { get; }
        
        protected LocalEventHandler(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Mapper = serviceProvider.GetRequiredService<IMapper>();
            CurrentUser = serviceProvider.GetRequiredService<ICurrentUser>();
        }
        
        public abstract Task HandleAsync(TEvent @event);
    }
}