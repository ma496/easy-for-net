using Backend.Features.Localization.Endpoints.Localization;

namespace Backend.Tests.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests for <see cref="LanguagesUpdateEndpoint"/>: upserting the stored language row, validation of
/// the enabled set and default, and the permission it requires.
/// </summary>
public class LanguagesUpdateTests(App app) : LocalizationTestsBase(app)
{
    [Fact]
    public async Task Second_Update_Replaces_Rather_Than_Duplicating()
    {
        var second = RequireSecondCulture();
        var roleId = await CreateRoleAsync(Allow.Localization_Update);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);

        var (firstResponse, _) = await client
            .PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, EmptyResponse>(new() { EnabledCultures = ["en"] });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (secondResponse, _) = await client.PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, EmptyResponse>(
            new() { EnabledCultures = ["en", second], DefaultCulture = second });
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rows = await DbContext.LanguageSettings
            .ToListAsync(TestContext.Current.CancellationToken);

        rows.Should().ContainSingle();
        rows[0].EnabledCultures.Should().BeEquivalentTo(["en", second]);
        rows[0].DefaultCulture.Should().Be(second);
    }

    [Fact]
    public async Task Uppercase_Enabled_Culture_And_Default_Are_Stored_As_Canonical_Lowercase()
    {
        var second = RequireSecondCulture();
        var roleId = await CreateRoleAsync(Allow.Localization_Update);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);

        var (response, _) = await client.PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, EmptyResponse>(
            new() { EnabledCultures = ["EN", second.ToUpperInvariant()], DefaultCulture = second.ToUpperInvariant() });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stored = await DbContext.LanguageSettings
            .SingleAsync(TestContext.Current.CancellationToken);
        stored.EnabledCultures.Should().BeEquivalentTo(["en", second],
            "an uppercase culture is stored under its canonical shipped code");
        stored.DefaultCulture.Should().Be(second);
    }

    [Fact]
    public async Task Empty_Enabled_Cultures_Is_Rejected()
    {
        await SetAuthTokenAsync();

        var (response, refusal) = await Client
            .PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, ProblemDetails>(new() { EnabledCultures = [] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Name.Equals("enabledCultures", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Duplicate_Enabled_Cultures_Is_Rejected()
    {
        await SetAuthTokenAsync();

        var (response, refusal) = await Client
            .PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, ProblemDetails>(new() { EnabledCultures = ["en", "en"] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Name.Equals("enabledCultures", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Unshipped_Enabled_Culture_Is_Rejected()
    {
        await SetAuthTokenAsync();

        var (response, refusal) = await Client
            .PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, ProblemDetails>(
                new() { EnabledCultures = ["en", "not-a-real-culture"] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Name.Equals("enabledCultures", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Default_Culture_Outside_The_Enabled_Set_Is_Rejected()
    {
        await SetAuthTokenAsync();

        var (response, refusal) = await Client
            .PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, ProblemDetails>(
                new() { EnabledCultures = ["en"], DefaultCulture = "not-a-real-culture" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Name.Equals("defaultCulture", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Missing_Permission_Is_Forbidden()
    {
        var roleId = await CreateRoleAsync(Allow.Localization_View);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);

        var (response, _) = await client
            .PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, ProblemDetails>(new() { EnabledCultures = ["en"] });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unauthenticated_Caller_Is_Refused()
    {
        var (response, _) = await Client
            .PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, ProblemDetails>(new() { EnabledCultures = ["en"] });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
