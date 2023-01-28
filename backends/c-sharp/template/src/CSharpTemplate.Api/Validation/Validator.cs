using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;
using FluentValidation;

namespace CSharpTemplate.Api.Validation;

[AttributeUsage(AttributeTargets.Parameter)]
public class ValidateAttribute : Attribute
{
}

public static class ValidationFilter
{
    public static EndpointFilterDelegate ValidationFilterFactory(EndpointFilterFactoryContext context, 
        EndpointFilterDelegate next)
    {
        var validationDescriptors = GetValidators(context.MethodInfo, context.ApplicationServices).ToList();

        return validationDescriptors.Any() 
            ? invocationContext => Validate(validationDescriptors, invocationContext, next) :
            next;
    }

    private static async ValueTask<object?> Validate(IEnumerable<ValidationDescriptor> validationDescriptors,
        EndpointFilterInvocationContext invocationContext, EndpointFilterDelegate next)
    {
        foreach (var descriptor in validationDescriptors)
        {
            var argument = invocationContext.Arguments[descriptor.ArgumentIndex];

            if (argument is null) continue;

            IDictionary<string, string[]> errors = new Dictionary<string, string[]>();
            
            // fluent validation
            if (descriptor.Validator != null)
            {
                var fluentValidationResult = await descriptor.Validator.ValidateAsync(
                    new ValidationContext<object>(argument)
                );
                if (!fluentValidationResult.IsValid)
                    errors = fluentValidationResult.ToDictionary();
            }

            // attribute validation
            var attributeValidationResult = new List<ValidationResult>();
            if (!Validator.TryValidateObject(argument, new ValidationContext(argument), attributeValidationResult, true))
            {
                attributeValidationResult.ForEach(r =>
                {
                    foreach (var memberName in r.MemberNames)
                    {
                        if (!errors.ContainsKey(memberName))
                        {
                            errors.Add(memberName, new[]{r.ErrorMessage ?? string.Empty});
                        }
                        else
                        {
                            var memberErrors = errors[memberName].ToList();
                            memberErrors.Add(r.ErrorMessage ?? string.Empty);
                            errors[memberName] = memberErrors.ToArray();
                        }
                    }
                });
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors, statusCode: (int)HttpStatusCode.UnprocessableEntity);
            }
        }

        return await next.Invoke(invocationContext);
    }

    static IEnumerable<ValidationDescriptor> GetValidators(MethodInfo methodInfo, IServiceProvider serviceProvider)
    {
        var parameters = methodInfo.GetParameters();

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];

            if (parameter.GetCustomAttribute<ValidateAttribute>() is null) continue;
            
            var validatorType = typeof(IValidator<>).MakeGenericType(parameter.ParameterType);

            // Note that FluentValidation validators needs to be registered as singleton

            var validator = serviceProvider.GetService(validatorType) as IValidator;
            yield return new ValidationDescriptor 
            { 
                ArgumentIndex = i, 
                ArgumentType = parameter.ParameterType,
                Validator = validator 
            };
        }
    }
}