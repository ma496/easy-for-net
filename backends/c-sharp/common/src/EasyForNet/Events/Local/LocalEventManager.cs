using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Events.Local
{
    public class LocalEventManager : ILocalEventManager
    {
        public IServiceProvider ServiceProvider { get; set; }

        public async Task RaiseAsync<TEvent>(TEvent @event)
            where TEvent : class
        {
            var eventHandlers = ServiceProvider.GetServices<ILocalEventHandler<TEvent>>();
            foreach (var eventHandler in eventHandlers)
            {
                await eventHandler.HandleAsync(@event);
            }
        }
    }
}