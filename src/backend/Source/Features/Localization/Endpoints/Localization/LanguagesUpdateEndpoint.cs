namespace Backend.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Core;

/// <summary>
/// This endpoint that handles <c>PUT /localization/languages</c> to upsert the acting scope's own
/// language settings row - which cultures it offers and which of them is the default.
/// </summary>
sealed class LanguagesUpdateEndpoint(ILocalizationService localizationService) : Endpoint<LanguagesUpdateRequest>
{
    public override void Configure()
    {
        Put("languages");
        Group<LocalizationGroup>();
        Permissions(Allow.Localization_Update);
    }

    public override async Task HandleAsync(LanguagesUpdateRequest request, CancellationToken cancellationToken)
    {
        await localizationService.SetLanguagesAsync(request.EnabledCultures, request.DefaultCulture, cancellationToken);
        await Send.NoContentAsync(cancellationToken);
    }
}

/// <summary>Request payload naming the cultures the acting scope enables and its default, if any.</summary>
sealed class LanguagesUpdateRequest
{
    public List<string> EnabledCultures { get; set; } = [];
    public string? DefaultCulture { get; set; }
}

/// <summary>
/// FluentValidation rules requiring a non-empty, duplicate-free set of shipped cultures, and a default
/// that - when named - is one of them. The resource store is injected so the shipped-culture check
/// reflects what is actually embedded.
/// </summary>
sealed class LanguagesUpdateValidator : Validator<LanguagesUpdateRequest>
{
    public LanguagesUpdateValidator(ILocalizationResourceStore resourceStore)
    {
        RuleFor(x => x.EnabledCultures)
            .NotEmpty()
            .Must(cultures => cultures.Distinct(StringComparer.OrdinalIgnoreCase).Count() == cultures.Count)
            .WithMessage("Enabled cultures must not repeat.")
            .Must(cultures => cultures.All(culture => resourceStore.ShippedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase)))
            .WithMessage("Every enabled culture must be shipped.");

        RuleFor(x => x.DefaultCulture)
            .Must((request, defaultCulture) => defaultCulture is null
                                                || request.EnabledCultures.Contains(defaultCulture, StringComparer.OrdinalIgnoreCase))
            .WithMessage("The default culture must be one of the enabled cultures.");
    }
}
