namespace Backend.Features.Tenancy.Core;

using System.Text.RegularExpressions;

/// <summary>
/// The single declaration of the tenant display-name and identifier rules, exposed as
/// FluentValidation rule-builder extensions so that platform tenant creation, tenant update and the
/// tenant a self-service sign-up creates all enforce one identical rule set instead of restating it.
/// </summary>
/// <remarks>
/// Both extensions pass a null, empty or white-space value through untouched, leaving the
/// missing-value case to the <c>NotEmpty()</c> each rule chain pairs them with:
/// <c>RuleFor(x =&gt; x.Identifier).NotEmpty().TenantIdentifier()</c>. That is deliberately
/// wider than the built-in <c>Length</c> and <c>Matches</c> validators, which skip only a null
/// value and would fail an empty string on top of <c>NotEmpty()</c> - so do not "simplify"
/// these rules back to them: rules run with the default <c>Continue</c> cascade, and an empty
/// name would then raise two failures both named <c>name</c>. A value that breaks both the
/// identifier length and the identifier shape does raise one failure for each, on purpose, so
/// the caller is told about both. Every failure is raised against the property itself, so the
/// response names the offending field.
/// </remarks>
[AllowOutside]
static class TenantValidationRules
{
    /// <summary>The fewest characters a tenant display name may carry once trimmed.</summary>
    public const int NameMinLength = 2;

    /// <summary>The most characters a tenant display name may carry once trimmed.</summary>
    public const int NameMaxLength = 100;

    /// <summary>The fewest characters a tenant identifier may carry once trimmed.</summary>
    public const int IdentifierMinLength = 3;

    /// <summary>The most characters a tenant identifier may carry once trimmed.</summary>
    public const int IdentifierMaxLength = 50;

    /// <summary>
    /// The identifier shape: lower-case ASCII letters, digits and hyphens, beginning and ending
    /// with a letter or a digit and never carrying two consecutive hyphens. Requiring every hyphen
    /// to be followed by at least one letter or digit is what rules out a leading hyphen, a
    /// trailing hyphen and a doubled hyphen in a single expression.
    /// </summary>
    public const string IdentifierPattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";

    static readonly Regex _identifierRegex = new(IdentifierPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Requires a tenant display name of <see cref="NameMinLength"/> to <see cref="NameMaxLength"/>
    /// characters once surrounding white-space is trimmed.
    /// </summary>
    /// <typeparam name="T">The request type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the display-name property.</param>
    /// <returns>The rule builder, so further rules can be chained onto the same property.</returns>
    public static IRuleBuilderOptions<T, string> TenantName<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .Must(value => value.IsNullOrWhiteSpace() || value.Trim().Length is >= NameMinLength and <= NameMaxLength)
            .WithMessage($"The tenant name must be between {NameMinLength} and {NameMaxLength} characters.");

    /// <summary>
    /// Requires a tenant identifier of <see cref="IdentifierMinLength"/> to
    /// <see cref="IdentifierMaxLength"/> characters once surrounding white-space is trimmed, made
    /// up only of lower-case ASCII letters, digits and single hyphens between them
    /// (<see cref="IdentifierPattern"/>). Length and shape are separate rules so that the message
    /// tells the caller which of the two the value broke.
    /// </summary>
    /// <typeparam name="T">The request type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the identifier property.</param>
    /// <returns>The rule builder, so further rules can be chained onto the same property.</returns>
    public static IRuleBuilderOptions<T, string> TenantIdentifier<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .Must(value => value.IsNullOrWhiteSpace() || value.Trim().Length is >= IdentifierMinLength and <= IdentifierMaxLength)
            .WithMessage($"The tenant identifier must be between {IdentifierMinLength} and {IdentifierMaxLength} characters.")
            .Must(value => value.IsNullOrWhiteSpace() || _identifierRegex.IsMatch(value.Trim()))
            .WithMessage("The tenant identifier must use lower-case letters, digits and single hyphens, and must begin and end with a letter or a digit.");
}