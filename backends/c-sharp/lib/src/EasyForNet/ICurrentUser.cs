namespace EasyForNet;

public interface ICurrentUser
{
    long UserId { get; }

    string Username { get; }
}