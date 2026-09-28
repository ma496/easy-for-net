namespace Backend.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Core;

/// <summary>
/// This endpoint that handles <c>PUT /localization/texts</c> to upsert the acting scope's override for
/// one shipped translation key.
/// </summary>
sealed class TextUpdateEndpoint(ILocalizationService localizationService) : Endpoint<TextUpdateRequest>
{
    public override void Configure()
    {
        Put("texts");
        Group<LocalizationGroup>();
        Permissions(Allow.Localization_Update);
    }

    public override async Task HandleAsync(TextUpdateRequest request, CancellationToken cancellationToken)
    {
        await localizationService.SetTextAsync(request.Culture, request.Key, request.Value.Trim(), cancellationToken);
        await Send.NoContentAsync(cancellationToken);
    }
}

/// <summary>Request payload naming the override to set and its value.</summary>
sealed class TextUpdateRequest
{
    public string Culture { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules requiring a shipped culture, a key the shipped English file actually
/// declares, and a non-empty value within the column's length. The resource store is injected so both
/// checks reflect what is actually shipped rather than a hard-coded list.
/// </summary>
sealed class TextUpdateValidator : Validator<TextUpdateRequest>
{
    public TextUpdateValidator(ILocalizationResourceStore resourceStore)
    {
        RuleFor(x => x.Culture)
            .NotEmpty()
            .Must(culture => resourceStore.ShippedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
            .WithMessage("The culture is not shipped.");
        RuleFor(x => x.Key)
            .NotEmpty()
            .Must(resourceStore.EnglishResources.ContainsKey)
            .WithMessage("The key does not exist in the shipped resources.");
        RuleFor(x => x.Value)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("The value must not be empty.")
            .MaximumLength(4000);
    }
}
