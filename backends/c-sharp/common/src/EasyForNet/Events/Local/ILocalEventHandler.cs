using System.Threading.Tasks;
using EasyForNet.Application.Dependencies;

namespace EasyForNet.Events.Local
{
    public interface ILocalEventHandler<in TEvent> : ITransientDependency
        where TEvent : class
    {
        Task HandleAsync(TEvent @event);
    }
}