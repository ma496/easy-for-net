namespace Backend.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Core;

/// <summary>
/// This endpoint that handles <c>GET /localization/languages</c> to report the acting scope's effective
/// language settings for the admin editor: every shipped culture, which are currently enabled, the
/// default, whether this scope has its own settings row, and what it would inherit without one.
/// </summary>
sealed class LanguagesGetEndpoint(ILocalizationService localizationService) : EndpointWithoutRequest<LanguagesGetResponse>
{
    public override void Configure()
    {
        Get("languages");
        Group<LocalizationGroup>();
        Permissions(Allow.Localization_View);
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var resolution = await localizationService.ResolveLanguagesAsync(cancellationToken);

        await Send.ResponseAsync(new LanguagesGetResponse
        {
            Languages = resolution.Languages,
            EnabledCultures = resolution.EnabledCultures,
            DefaultCulture = resolution.DefaultCulture,
            IsInherited = resolution.IsInherited,
            InheritedEnabledCultures = resolution.InheritedEnabledCultures,
            InheritedDefaultCulture = resolution.InheritedDefaultCulture,
        }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Response payload reporting every shipped culture, the acting scope's effective enabled set and
/// default, whether that comes from the scope's own row, and what it would inherit without one.
/// </summary>
public sealed class LanguagesGetResponse
{
    public List<LanguageDto> Languages { get; set; } = [];
    public List<string> EnabledCultures { get; set; } = [];
    public string? DefaultCulture { get; set; }
    public bool IsInherited { get; set; }
    public List<string> InheritedEnabledCultures { get; set; } = [];
    public string? InheritedDefaultCulture { get; set; }
}
