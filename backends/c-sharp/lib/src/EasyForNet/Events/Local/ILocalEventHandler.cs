using System.Threading.Tasks;

namespace EasyForNet.Events.Local
{
    public interface ILocalEventHandler<in TEvent>
        where TEvent : class
    {
        Task HandleAsync(TEvent @event);
    }
}