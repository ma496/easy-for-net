using Backend.Features.Localization.Endpoints.Localization;

namespace Backend.Tests.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests for <see cref="TextDeleteEndpoint"/>: removing a text override, idempotency, the
/// canonical culture an override is found under, and the permission it requires.
/// </summary>
public class TextDeleteTests(App app) : LocalizationTestsBase(app)
{
    [Fact]
    public async Task Delete_Of_A_Key_With_No_Override_Is_Idempotent()
    {
        var roleId = await CreateRoleAsync(Allow.Localization_Update);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);

        var (response, _) = await client
            .DELETEAsync<TextDeleteEndpoint, TextDeleteRequest, EmptyResponse>(new() { Culture = Culture, Key = AnyShippedKey() });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, "a key with nothing to remove still answers success");
    }

    [Fact]
    public async Task Uppercase_Culture_Deletes_The_Canonically_Stored_Override()
    {
        var roleId = await CreateRoleAsync(Allow.Localization_Update);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);
        var key = AnyShippedKey();

        await SetTextAsync(Culture, key, "will be removed");

        var (response, _) = await client
            .DELETEAsync<TextDeleteEndpoint, TextDeleteRequest, EmptyResponse>(new() { Culture = Culture.ToUpperInvariant(), Key = key });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await DbContext.LocalizationTexts
            .AnyAsync(x => x.Key == key, TestContext.Current.CancellationToken))
            .Should().BeFalse("an uppercase culture is mapped to the same canonical culture the override was stored under");
    }

    [Fact]
    public async Task Unshipped_Culture_Is_Rejected()
    {
        await SetAuthTokenAsync();

        var (response, refusal) = await Client
            .DELETEAsync<TextDeleteEndpoint, TextDeleteRequest, ProblemDetails>(
                new() { Culture = "not-a-real-culture", Key = AnyShippedKey() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Name.Equals("culture", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Missing_Permission_Is_Forbidden()
    {
        var roleId = await CreateRoleAsync(Allow.Localization_View);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);

        var (response, _) = await client
            .DELETEAsync<TextDeleteEndpoint, TextDeleteRequest, ProblemDetails>(new() { Culture = Culture, Key = AnyShippedKey() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
