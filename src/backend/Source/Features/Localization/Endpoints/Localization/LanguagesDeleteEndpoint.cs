namespace Backend.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Core;

/// <summary>
/// This endpoint that handles <c>DELETE /localization/languages</c> to remove the acting scope's own
/// language settings row, reverting it to whatever it inherits. Idempotent: a scope with no row of its
/// own answers 204 exactly as one that had a row just removed does.
/// </summary>
sealed class LanguagesDeleteEndpoint(ILocalizationService localizationService) : EndpointWithoutRequest<EmptyResponse>
{
    public override void Configure()
    {
        Delete("languages");
        Group<LocalizationGroup>();
        Permissions(Allow.Localization_Update);
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        await localizationService.DeleteLanguagesAsync(cancellationToken);
        await Send.NoContentAsync(cancellationToken);
    }
}
