namespace EasyForNet.Application.Common.Exceptions;

public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException()
        : base("You are not allowed to access this resource.")
    {

    }
}
