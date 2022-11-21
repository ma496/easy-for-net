using EasyForNet.Application.Dependencies;

namespace EasyForNet;

public interface ICurrentUser
{
    long UserId { get; }

    string Username { get; }
}

internal class CurrentUser : ICurrentUser, ITransientDependency
{
    public long UserId => 0;
    public string Username => "Anonymous";
}