namespace Backend.Tests.ErrorHandling;

using System.Security.Claims;
using Backend.Features.Localization.Core;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Covers <see cref="ErrorLocalization.LocalizeErrorAsync"/>'s own failure handling against a fake
/// <see cref="IErrorMessageLocalizer"/> that always throws - the one branch of the mechanism a
/// request-level test cannot provoke on demand, since it would need the real lookup (the database
/// behind it, in particular) to actually fail.
/// </summary>
public class ErrorLocalizationFallbackTests(App app) : AppTestsBase(app)
{
    /// <summary>Stands in for a lookup that cannot complete - the database being unreachable, say.</summary>
    private sealed class ThrowingLocalizer : IErrorMessageLocalizer
    {
        public Task<string?> TryLocalizeErrorAsync(
            string requestedCulture, string errorCode, string propertyName, Guid? sessionTenantId = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("the database is unreachable");
    }

    [Fact]
    public async Task A_Localization_Failure_Falls_Back_To_The_Shipped_English_Text_For_The_Code()
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = App.Services,
            User = new ClaimsPrincipal(new ClaimsIdentity()),
        };

        var reason = await new ThrowingLocalizer().LocalizeErrorAsync(
            httpContext,
            ErrorCodes.UsernameAlreadyExists.Value,
            "username",
            "a fallback this test never expects to see",
            TestContext.Current.CancellationToken);

        var resourceStore = Service<ILocalizationResourceStore>();
        reason.Should().Be(resourceStore.EnglishResources["error.server.usernameAlreadyExists"],
            "a broken localization lookup must not turn an already-classified, coded error into an unrelated " +
            "failure of its own - it falls back to the code's own shipped English text, read with no database " +
            "involved, rather than to whatever the caller happened to pass as its own fallback");
    }

    [Fact]
    public async Task A_Localization_Failure_Keeps_An_Uncoded_Rules_Own_Message()
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = App.Services,
            User = new ClaimsPrincipal(new ClaimsIdentity()),
        };

        var reason = await new ThrowingLocalizer().LocalizeErrorAsync(
            httpContext,
            "NotEmptyValidator",
            "username",
            "'username' must not be empty.",
            TestContext.Current.CancellationToken);

        reason.Should().Be("'username' must not be empty.",
            "a plain FluentValidation rule has no error.server.* text to fall back to, so its own message stands " +
            "rather than being replaced by the bare rule code");
    }
}
