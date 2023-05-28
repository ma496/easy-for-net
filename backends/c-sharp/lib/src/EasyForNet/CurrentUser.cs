using EasyForNet.Application.Dependencies;

namespace EasyForNet;

internal class CurrentUser : ICurrentUser, ITransientDependency
{
    public long UserId => 0;
    public string Username => "Anonymous";
}