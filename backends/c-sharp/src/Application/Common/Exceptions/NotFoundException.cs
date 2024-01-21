using EasyForNet.Domain.Exceptions;

namespace EasyForNet.Application.Common.Exceptions;

public class NotFoundException : AppException
{
    public NotFoundException()
        : base("The specified resource was not found.", AppErrorCodes.NotFound)
    {
    }

    public NotFoundException(string message)
        : base(message, AppErrorCodes.NotFound)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException, AppErrorCodes.NotFound)
    {
    }

    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.", AppErrorCodes.NotFound)
    {
    }
}
