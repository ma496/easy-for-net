using System.Threading.Tasks;
using EasyForNet.Application.Dependencies;
using EasyForNet.Common;
using Microsoft.Extensions.DependencyInjection;

namespace EasyForNet.Events.Local;

public class LocalEventManager : CommonThings, ILocalEventManager, ITransientDependency
{
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