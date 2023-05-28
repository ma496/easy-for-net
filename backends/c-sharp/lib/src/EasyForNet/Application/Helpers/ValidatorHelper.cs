using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using EasyForNet.Exceptions.UserFriendly;
using FluentValidation;
using ValidationException = EasyForNet.Exceptions.UserFriendly.ValidationException;

namespace EasyForNet.Application.Helpers;

public static class ValidatorHelper
{
    public static void Validate<T>(T obj)
        where T : class
    {
        Guard.Against.Null(obj, nameof(obj));

        List<ValidationResult> validationResults = new();
        var isValidate = Validator.TryValidateObject(obj, new ValidationContext(obj), validationResults, true);
        if (!isValidate)
        {
            List<ValidationError> validationErrors = new();
            foreach (var vr in validationResults)
            {
                foreach (var mn in vr.MemberNames)
                {
                    validationErrors.Add(new ValidationError(mn, vr.ErrorMessage));
                }
            }

            throw new ValidationException(validationErrors);
        }
    }

    public static void Validate<T>(List<T> collection)
        where T : class
    {
        Guard.Against.Null(collection, nameof(collection));

        List<ValidationError> validationErrors = new();
        foreach (var obj in collection)
        {
            var index = collection.IndexOf(obj);
            List<ValidationResult> validationResults = new();
            var isValidate = Validator.TryValidateObject(obj, new ValidationContext(obj), validationResults, true);
            if (!isValidate)
            {
                foreach (var vr in validationResults)
                {
                    foreach (var mn in vr.MemberNames)
                    {
                        validationErrors.Add(new ValidationError($"[{index}].{mn}", vr.ErrorMessage));
                    }
                }
            }
        }

        if (validationErrors.Count > 0)
            throw new ValidationException(validationErrors);
    }

    public static async Task ValidateAsync<T>(T obj, AbstractValidator<T> validator)
        where T : class
    {
        Guard.Against.Null(obj, nameof(obj));
        Guard.Against.Null(validator, nameof(validator));

        var validationResult = await validator.ValidateAsync(obj);
        if (!validationResult.IsValid)
        {
            List<ValidationError> validationErrors = new();
            validationResult.Errors.ForEach(e =>
                validationErrors.Add(new ValidationError(e.PropertyName, e.ErrorMessage)));
            throw new ValidationException(validationErrors);
        }
    }

    public static async Task ValidateAsync<T>(List<T> collection, AbstractValidator<T> validator)
        where T : class
    {
        Guard.Against.Null(collection, nameof(collection));
        Guard.Against.Null(validator, nameof(validator));

        List<ValidationError> validationErrors = new();
        foreach (var obj in collection)
        {
            var index = collection.IndexOf(obj);
            var validationResult = await validator.ValidateAsync(obj);
            if (!validationResult.IsValid)
            {
                validationResult.Errors.ForEach(e =>
                    validationErrors.Add(new ValidationError($"[{index}].{e.PropertyName}", e.ErrorMessage)));
            }
        }

        if (validationErrors.Count > 0)
            throw new ValidationException(validationErrors);
    }
}