namespace EasyForNet.Domain.Exceptions;

public class AppException : Exception
{
    public AppException(AppErrorCodes code = AppErrorCodes.BadRequest)
        : base("Bad request.")
    {
        Code = code;
    }

    public AppException(string message, AppErrorCodes code = AppErrorCodes.BadRequest)
        : base(message)
    {
        Code = code;
    }

    public AppException(string message, Exception innerException, AppErrorCodes code = AppErrorCodes.BadRequest)
        : base(message, innerException)
    {
        Code = code;
    }

    public AppException(string message, IDictionary<string, string[]> errors)
        : base(message)
    {
        Code = AppErrorCodes.BadRequest;
        Errors = errors;
    }

    public AppErrorCodes Code { get; private set; }

    public IDictionary<string, string[]> Errors { get; private set; } = new Dictionary<string, string[]>();

    public int GetHttpStatusCode()
    {
        switch(Code)
        {
            case AppErrorCodes.Unauthorized:
                return 401;
            case AppErrorCodes.Forbidden:
                return 403;
            case AppErrorCodes.NotFound:
                return 404;
            default:
                return 400;
        }
    }
}
