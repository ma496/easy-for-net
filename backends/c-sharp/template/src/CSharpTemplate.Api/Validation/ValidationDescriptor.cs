using FluentValidation;

namespace CSharpTemplate.Api.Validation;

public class ValidationDescriptor
{
    public required int ArgumentIndex { get; init; }
    public required Type ArgumentType { get; init; }
    public required IValidator? Validator { get; init; }
}