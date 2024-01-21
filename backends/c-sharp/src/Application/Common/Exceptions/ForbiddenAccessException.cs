using EasyForNet.Domain.Exceptions;

namespace EasyForNet.Application.Common.Exceptions;

public class ForbiddenAccessException : AppException
{
    public ForbiddenAccessException()
        : base("You are not allowed to access this resource.", AppErrorCodes.Forbidden)
    {

    }
}
