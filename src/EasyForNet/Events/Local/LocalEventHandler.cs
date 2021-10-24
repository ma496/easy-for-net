using System.Threading.Tasks;

namespace EasyForNet.Events.Local
{
    public abstract class LocalEventHandler<TEvent> : ILocalEventHandler<TEvent>
        where TEvent : class
    {
        public abstract Task HandleAsync(TEvent @event);
    }
}