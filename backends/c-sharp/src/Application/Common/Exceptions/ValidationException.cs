using EasyForNet.Domain.Exceptions;
using FluentValidation.Results;

namespace EasyForNet.Application.Common.Exceptions;

public class ValidationException : AppException
{
    public ValidationException()
        : base("One or more validation failures have occurred.", AppErrorCodes.BadRequest)
    {
    }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation failures have occurred.", GetErrors(failures))
    {
    }

    private static IDictionary<string, string[]> GetErrors(IEnumerable<ValidationFailure> failures)
    {
        return failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());
    }
}
