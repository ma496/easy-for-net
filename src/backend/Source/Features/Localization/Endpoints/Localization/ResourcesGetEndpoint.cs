namespace Backend.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Core;

/// <summary>
/// This endpoint that handles <c>GET /localization/resources/{culture}</c> to serve one culture's
/// translation resources, merged with the acting scope's overrides, to the web app's server-side
/// dictionary loader.
/// </summary>
/// <remarks>
/// Anonymous by design: the web app fetches this before it knows who, if anyone, is signed in. A
/// caller that does carry a valid session still sees its overrides - a signed-in visit resolves the
/// tenant it acts in exactly as any other request does - and a request with no session at all sees the
/// platform's overrides only, there being no tenant to prefer over them. An unknown or disabled culture
/// never fails the request: the same fallback that governs a merely unrequested culture governs a
/// nonsensical one, and <see cref="ResolvedResources.Culture"/> always names the culture actually
/// served so the caller can tell.
/// </remarks>
sealed class ResourcesGetEndpoint(ILocalizationService localizationService) : Endpoint<ResourcesGetRequest, ResourcesGetResponse>
{
    public override void Configure()
    {
        Get("resources/{culture}");
        Group<LocalizationGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(ResourcesGetRequest request, CancellationToken cancellationToken)
    {
        var resolved = await localizationService.ResolveResourcesAsync(request.Culture, cancellationToken);

        await Send.ResponseAsync(new ResourcesGetResponse
        {
            Culture = resolved.Culture,
            DefaultCulture = resolved.DefaultCulture,
            Languages = resolved.Languages,
            Resources = resolved.Resources,
        }, cancellation: cancellationToken);
    }
}

/// <summary>Request payload naming the culture the caller asked for.</summary>
sealed class ResourcesGetRequest
{
    public string Culture { get; set; } = null!;
}

/// <summary>
/// Response payload carrying the culture actually served, its configured default (if any), the
/// enabled languages, and the flat, dotted-key resource dictionary for the served culture.
/// </summary>
public sealed class ResourcesGetResponse
{
    public string Culture { get; set; } = null!;
    public string? DefaultCulture { get; set; }
    public List<LanguageDto> Languages { get; set; } = [];
    public Dictionary<string, string> Resources { get; set; } = [];
}
