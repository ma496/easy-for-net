namespace Backend.Features.Tenancy.Core;

/// <summary>
/// The single declaration of the edition name and description rules, exposed as FluentValidation
/// rule-builder extensions so that edition creation and edition update enforce one identical rule set
/// instead of restating it.
/// </summary>
/// <remarks>
/// Both extensions pass a null, empty or white-space value through untouched, leaving the
/// missing-value case to the <c>NotEmpty()</c> each rule chain pairs them with, in the same shape
/// <see cref="TenantValidationRules"/> uses and for the same reason: rules run with the default
/// <c>Continue</c> cascade, so an empty name would otherwise raise two failures both named
/// <c>name</c>.
/// </remarks>
[AllowOutside]
static class EditionValidationRules
{
    /// <summary>The fewest characters an edition name may carry once trimmed.</summary>
    public const int NameMinLength = 2;

    /// <summary>The most characters an edition name may carry once trimmed.</summary>
    public const int NameMaxLength = 128;

    /// <summary>The most characters an edition description may carry once trimmed.</summary>
    public const int DescriptionMaxLength = 512;

    /// <summary>
    /// Applies the edition display-name length rule to a string property.
    /// </summary>
    /// <typeparam name="T">The request type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule chain being extended.</param>
    /// <returns>The rule chain, so it reads as one statement.</returns>
    public static IRuleBuilderOptions<T, string> EditionName<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .Must(name => string.IsNullOrWhiteSpace(name)
                          || (name.Trim().Length >= NameMinLength && name.Trim().Length <= NameMaxLength))
            .WithMessage($"The name must be between {NameMinLength} and {NameMaxLength} characters.");

    /// <summary>
    /// Applies the edition description length rule to an optional string property.
    /// </summary>
    /// <typeparam name="T">The request type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule chain being extended.</param>
    /// <returns>The rule chain, so it reads as one statement.</returns>
    public static IRuleBuilderOptions<T, string?> EditionDescription<T>(this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder
            .Must(description => string.IsNullOrWhiteSpace(description)
                                 || description.Trim().Length <= DescriptionMaxLength)
            .WithMessage($"The description must be at most {DescriptionMaxLength} characters.");
}
