namespace Backend.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Core;

/// <summary>
/// This endpoint that handles <c>DELETE /localization/texts</c> to remove the acting scope's override
/// for one key, reverting it to whatever it inherits. Idempotent: a key with no override in this scope
/// answers 204 exactly as one that had an override just removed does.
/// </summary>
sealed class TextDeleteEndpoint(ILocalizationService localizationService) : Endpoint<TextDeleteRequest>
{
    public override void Configure()
    {
        Delete("texts");
        Group<LocalizationGroup>();
        Permissions(Allow.Localization_Update);
    }

    public override async Task HandleAsync(TextDeleteRequest request, CancellationToken cancellationToken)
    {
        await localizationService.DeleteTextAsync(request.Culture, request.Key, cancellationToken);
        await Send.NoContentAsync(cancellationToken);
    }
}

/// <summary>Request payload naming the override to remove, bound from the query string.</summary>
sealed class TextDeleteRequest
{
    public string Culture { get; set; } = null!;
    public string Key { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules requiring a shipped culture and a non-empty key. The resource store is
/// injected so the shipped-culture check reflects what is actually embedded, exactly as the list and
/// update validators check it.
/// </summary>
sealed class TextDeleteValidator : Validator<TextDeleteRequest>
{
    public TextDeleteValidator(ILocalizationResourceStore resourceStore)
    {
        RuleFor(x => x.Culture)
            .NotEmpty()
            .Must(culture => resourceStore.ShippedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
            .WithMessage("The culture is not shipped.");
        RuleFor(x => x.Key).NotEmpty();
    }
}
