using EasyForNet;

namespace CSharpTemplate.Api;

public class CurrentUser : ICurrentUser
{
    public long UserId { get; }

    public string Username { get; } = string.Empty;
}