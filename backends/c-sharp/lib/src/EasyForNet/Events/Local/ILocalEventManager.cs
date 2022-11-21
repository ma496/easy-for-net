using System.Threading.Tasks;

namespace EasyForNet.Events.Local;

public interface ILocalEventManager
{
    Task RaiseAsync<TEvent>(TEvent @event)
        where TEvent : class;
}