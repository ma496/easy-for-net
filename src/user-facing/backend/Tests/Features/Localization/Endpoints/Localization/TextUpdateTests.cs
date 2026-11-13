using Backend.Features.Localization.Endpoints.Localization;

namespace Backend.Tests.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests for <see cref="TextUpdateEndpoint"/>: upserting a text override, validation of
/// the culture, key and value, and the permission it requires.
/// </summary>
public class TextUpdateTests(App app) : LocalizationTestsBase(app)
{
    [Fact]
    public async Task Second_Update_Replaces_Rather_Than_Duplicating()
    {
        var roleId = await CreateRoleAsync(Allow.Localization_Update);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);
        var key = AnyShippedKey();

        await client.PUTAsync<TextUpdateEndpoint, TextUpdateRequest, EmptyResponse>(new() { Culture = Culture, Key = key, Value = "first" });
        await client.PUTAsync<TextUpdateEndpoint, TextUpdateRequest, EmptyResponse>(new() { Culture = Culture, Key = key, Value = "second" });

        var rows = await DbContext.LocalizationTexts
            .Where(x => x.Culture == Culture && x.Key == key)
            .ToListAsync(TestContext.Current.CancellationToken);

        rows.Should().ContainSingle().Which.Value.Should().Be("second");
    }

    [Fact]
    public async Task Uppercase_Culture_Is_Stored_As_Canonical_Lowercase()
    {
        var roleId = await CreateRoleAsync(Allow.Localization_Update);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);
        var key = AnyShippedKey();

        var (response, _) = await client.PUTAsync<TextUpdateEndpoint, TextUpdateRequest, EmptyResponse>(
            new() { Culture = Culture.ToUpperInvariant(), Key = key, Value = "canonical value" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stored = await DbContext.LocalizationTexts
            .SingleAsync(x => x.Key == key, TestContext.Current.CancellationToken);
        stored.Culture.Should().Be(Culture, "an uppercase culture is stored under its canonical shipped code");
    }

    [Fact]
    public async Task Unshipped_Culture_Is_Rejected()
    {
        await SetAuthTokenAsync();
        var key = AnyShippedKey();

        var (response, refusal) = await Client
            .PUTAsync<TextUpdateEndpoint, TextUpdateRequest, ProblemDetails>(
                new() { Culture = "not-a-real-culture", Key = key, Value = "value" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Name.Equals("culture", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Key_Not_In_The_Shipped_English_File_Is_Rejected()
    {
        await SetAuthTokenAsync();

        var (response, refusal) = await Client
            .PUTAsync<TextUpdateEndpoint, TextUpdateRequest, ProblemDetails>(
                new() { Culture = Culture, Key = $"no.such.key.{Guid.NewGuid():N}", Value = "value" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Name.Equals("key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Blank_Value_Is_Rejected()
    {
        await SetAuthTokenAsync();
        var key = AnyShippedKey();

        var (response, refusal) = await Client
            .PUTAsync<TextUpdateEndpoint, TextUpdateRequest, ProblemDetails>(new() { Culture = Culture, Key = key, Value = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Name.Equals("value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Overlong_Value_Is_Rejected()
    {
        await SetAuthTokenAsync();
        var key = AnyShippedKey();

        var (response, refusal) = await Client
            .PUTAsync<TextUpdateEndpoint, TextUpdateRequest, ProblemDetails>(
                new() { Culture = Culture, Key = key, Value = new string('x', 4001) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Name.Equals("value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Missing_Permission_Is_Forbidden()
    {
        var roleId = await CreateRoleAsync(Allow.Localization_View);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);
        var key = AnyShippedKey();

        var (response, _) = await client
            .PUTAsync<TextUpdateEndpoint, TextUpdateRequest, ProblemDetails>(new() { Culture = Culture, Key = key, Value = "value" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unauthenticated_Caller_Is_Refused()
    {
        var key = AnyShippedKey();

        var (response, _) = await Client
            .PUTAsync<TextUpdateEndpoint, TextUpdateRequest, ProblemDetails>(new() { Culture = Culture, Key = key, Value = "value" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
