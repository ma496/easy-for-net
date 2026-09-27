namespace Backend.Tests.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests for <see cref="TextUpdateEndpoint"/>: upserting the acting scope's own override, validation of
/// the culture, key and value, and the permission it requires.
/// </summary>
public class TextUpdateTests(App app) : LocalizationTestsBase(app)
{
    [Fact]
    public async Task Valid_Update_Upserts_The_Acting_Scopes_Own_Override_Trimmed()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_Update);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
        var key = AnyShippedKey();

        var (response, _) = await client
            .PUTAsync<TextUpdateEndpoint, TextUpdateRequest, EmptyResponse>(
                new() { Culture = Culture, Key = key, Value = "  padded value  " });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stored = await DbContext.LocalizationTexts.AcrossAllTenants()
            .SingleAsync(x => x.TenantId == tenant.Id && x.Culture == Culture && x.Key == key, TestContext.Current.CancellationToken);
        stored.Value.Should().Be("padded value", "the value is trimmed before it is stored");
    }

    [Fact]
    public async Task Second_Update_Replaces_Rather_Than_Duplicating()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_Update);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
        var key = AnyShippedKey();

        await client.PUTAsync<TextUpdateEndpoint, TextUpdateRequest, EmptyResponse>(new() { Culture = Culture, Key = key, Value = "first" });
        await client.PUTAsync<TextUpdateEndpoint, TextUpdateRequest, EmptyResponse>(new() { Culture = Culture, Key = key, Value = "second" });

        var rows = await DbContext.LocalizationTexts.AcrossAllTenants()
            .Where(x => x.TenantId == tenant.Id && x.Culture == Culture && x.Key == key)
            .ToListAsync(TestContext.Current.CancellationToken);

        rows.Should().ContainSingle().Which.Value.Should().Be("second");
    }

    [Fact]
    public async Task Uppercase_Culture_Is_Stored_As_Canonical_Lowercase()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_Update);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
        var key = AnyShippedKey();

        var (response, _) = await client.PUTAsync<TextUpdateEndpoint, TextUpdateRequest, EmptyResponse>(
            new() { Culture = Culture.ToUpperInvariant(), Key = key, Value = "canonical value" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stored = await DbContext.LocalizationTexts.AcrossAllTenants()
            .SingleAsync(x => x.TenantId == tenant.Id && x.Key == key, TestContext.Current.CancellationToken);
        stored.Culture.Should().Be(Culture, "an uppercase culture is stored under its canonical shipped code");
    }

    [Fact]
    public async Task Platform_Administrator_Sets_A_Platform_Override()
    {
        await SetPlatformAdminAuthTokenAsync();
        var key = AnyShippedKey();

        var (response, _) = await Client
            .PUTAsync<TextUpdateEndpoint, TextUpdateRequest, EmptyResponse>(new() { Culture = Culture, Key = key, Value = "platform wide" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stored = await DbContext.LocalizationTexts.AcrossAllTenants()
            .SingleAsync(x => x.TenantId == null && x.Culture == Culture && x.Key == key, TestContext.Current.CancellationToken);
        stored.Value.Should().Be("platform wide");
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
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
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
