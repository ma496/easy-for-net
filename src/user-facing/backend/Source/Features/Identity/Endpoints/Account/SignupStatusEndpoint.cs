using Backend.Features.Settings.Core;

namespace Backend.Features.Identity.Endpoints.Account;

/// <summary>
/// Anonymous GET endpoint that says whether sign-up is open, so the web app can offer or withhold the
/// sign-up form before a visitor fills it in. <see cref="SignupEndpoint"/> stays the authority: it
/// refuses with 403 <c>signupDisabled</c> whatever this answered a moment earlier.
/// </summary>
sealed class SignupStatusEndpoint(ISettingProvider settingProvider) : EndpointWithoutRequest<SignupStatusResponse>
{
    public override void Configure()
    {
        Get("signup-status");
        Group<AccountGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var generalSettings = await settingProvider.GetAsync<GeneralSettings>(cancellationToken);
        await Send.ResponseAsync(new SignupStatusResponse { IsSignupEnabled = generalSettings.IsSignupEnabled }, cancellation: cancellationToken);
    }
}

/// <summary>Whether a visitor may create an account.</summary>
sealed class SignupStatusResponse
{
    /// <summary><c>General.IsSignupEnabled</c>: true when <c>POST /account/signup</c> accepts a new account.</summary>
    public bool IsSignupEnabled { get; set; }
}
