using EasyForNet;
using EasyForNet.Application.Dependencies;

namespace CSharpTemplate.Api;

public class CurrentUser : ICurrentUser, ITransientDependency
{
    public long UserId { get; }

    public string Username { get; } = string.Empty;
}