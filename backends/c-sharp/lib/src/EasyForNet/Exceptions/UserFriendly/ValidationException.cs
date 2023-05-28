using System.Collections.Generic;
using System.Text;

namespace EasyForNet.Exceptions.UserFriendly;

public class ValidationException : UserFriendlyException
{
    public List<ValidationError> ValidationErrors { get; }

    public ValidationException(List<ValidationError> validationErrors) : base(GetMessage(validationErrors))
    {
        ValidationErrors = validationErrors;
    }

    private static string GetMessage(List<ValidationError> validationErrors)
    {
        var messageBuilder = new StringBuilder();
        foreach (var ve in validationErrors)
        {
            messageBuilder.AppendLine(ve.ErrorMessage);
        }

        return messageBuilder.ToString();
    }
}