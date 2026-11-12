namespace Backend.Features.Localization.Endpoints.Localization;

/// <summary>
/// This route group that prefixes every localization endpoint - resources, texts and language
/// settings - with the <c>localization</c> segment.
/// </summary>
sealed class LocalizationGroup : Group
{
    public LocalizationGroup()
    {
        Configure("localization", ep => {});
    }
}
