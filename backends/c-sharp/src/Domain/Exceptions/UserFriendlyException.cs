namespace EasyForNet.Domain.Exceptions;

using System;

public class UserFriendlyException : Exception
{
    public int Code { get; }

    public UserFriendlyException()
    {
    }

    public UserFriendlyException(string message)
        : base(message)
    {
    }

    public UserFriendlyException(string message, int code)
        : base(message)
    {
        Code = code;
    }

    public UserFriendlyException(string message, int code, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}
