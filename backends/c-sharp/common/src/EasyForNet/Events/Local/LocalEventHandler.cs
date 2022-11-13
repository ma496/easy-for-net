using System.Threading.Tasks;
using AutoMapper;

namespace EasyForNet.Events.Local
{
    public abstract class LocalEventHandler<TEvent> : ILocalEventHandler<TEvent>
        where TEvent : class
    {
        protected IMapper Mapper { get; set; }
        protected ICurrentUser CurrentUser { get; set; }

        public abstract Task HandleAsync(TEvent @event);
    }
}