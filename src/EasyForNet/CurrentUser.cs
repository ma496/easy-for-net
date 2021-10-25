using EasyForNet.Application.Dependencies;

namespace EasyForNet
{
    public interface ICurrentUser : ITransientDependency
    {
        long UserId { get; }

        string Username { get; }
    }

    internal class CurrentUser : ICurrentUser
    {
        public long UserId => 0;
        public string Username => "Anonymous";
    }
}