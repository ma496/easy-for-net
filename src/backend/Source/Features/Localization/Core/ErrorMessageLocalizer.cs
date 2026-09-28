namespace Backend.Features.Localization.Core;

/// <summary>
/// The narrow surface the global error plumbing - outside the <c>Localization</c> feature, under
/// <c>Backend.Processors</c>, <c>Backend.ErrorHandling</c> and <c>Program.cs</c> - depends on to
/// translate a coded error's message, without becoming a forbidden cross-feature dependency on the
/// whole of <see cref="ILocalizationService"/> (the text-override editor, language administration, …).
/// Marked <see cref="AllowOutsideAttribute"/> for exactly that: this interface alone is the feature's
/// published error-localization contract.
/// </summary>
[AllowOutside]
public interface IErrorMessageLocalizer
{
    /// <inheritdoc cref="ILocalizationService.TryLocalizeErrorAsync"/>
    Task<string?> TryLocalizeErrorAsync(string requestedCulture, string errorCode, string propertyName, Guid? sessionTenantId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thin adapter over <see cref="ILocalizationService"/> that exposes nothing beyond
/// <see cref="IErrorMessageLocalizer"/>, so a caller outside the feature can be given this narrow type
/// without ever being handed the full service.
/// </summary>
[NoDirectUse]
internal sealed class ErrorMessageLocalizer(ILocalizationService localizationService) : IErrorMessageLocalizer
{
    /// <inheritdoc />
    public Task<string?> TryLocalizeErrorAsync(string requestedCulture, string errorCode, string propertyName, Guid? sessionTenantId = null, CancellationToken cancellationToken = default)
        => localizationService.TryLocalizeErrorAsync(requestedCulture, errorCode, propertyName, sessionTenantId, cancellationToken);
}
