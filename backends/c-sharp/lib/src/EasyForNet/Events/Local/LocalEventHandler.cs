using EasyForNet.Application.Dependencies;
using EasyForNet.Common;
using System.Threading.Tasks;

namespace EasyForNet.Events.Local
{
    public abstract class LocalEventHandler<TEvent> : CommonThings, ILocalEventHandler<TEvent>, ITransientDependency
        where TEvent : class
    {
        public abstract Task HandleAsync(TEvent @event);
    }
}