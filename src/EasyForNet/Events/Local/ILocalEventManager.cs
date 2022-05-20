using System.Threading.Tasks;
using EasyForNet.Application.Dependencies;

namespace EasyForNet.Events.Local
{
    public interface ILocalEventManager : ITransientDependency
    {
        Task RaiseAsync<TEvent>(TEvent @event)
            where TEvent : class;
    }
}