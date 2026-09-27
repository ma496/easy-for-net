namespace Backend.Tests.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests for <see cref="TextDeleteEndpoint"/>: removing the acting scope's own override, idempotency,
/// leaving the platform layer untouched, and the permission it requires.
/// </summary>
public class TextDeleteTests(App app) : LocalizationTestsBase(app)
{
    [Fact]
    public async Task Delete_Removes_The_Acting_Scopes_Override()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_Update);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
        var key = AnyShippedKey();

        await SetTenantTextAsync(tenant.Id, Culture, key, "will be removed");

        var (response, _) = await client
            .DELETEAsync<TextDeleteEndpoint, TextDeleteRequest, EmptyResponse>(new() { Culture = Culture, Key = key });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await DbContext.LocalizationTexts.AcrossAllTenants()
            .AnyAsync(x => x.TenantId == tenant.Id && x.Culture == Culture && x.Key == key, TestContext.Current.CancellationToken))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Delete_Of_A_Key_With_No_Override_Is_Idempotent()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_Update);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);

        var (response, _) = await client
            .DELETEAsync<TextDeleteEndpoint, TextDeleteRequest, EmptyResponse>(new() { Culture = Culture, Key = AnyShippedKey() });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, "a key with nothing to remove still answers success");
    }

    [Fact]
    public async Task Delete_Leaves_The_Platform_Layer_Untouched()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_Update);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
        var key = AnyShippedKey();

        await SetPlatformTextAsync(Culture, key, "platform stays");
        await SetTenantTextAsync(tenant.Id, Culture, key, "tenant goes");

        await client.DELETEAsync<TextDeleteEndpoint, TextDeleteRequest, EmptyResponse>(new() { Culture = Culture, Key = key });

        (await DbContext.LocalizationTexts.AcrossAllTenants()
            .AnyAsync(x => x.TenantId == null && x.Culture == Culture && x.Key == key, TestContext.Current.CancellationToken))
            .Should().BeTrue("removing the tenant's own override must not reach the platform's");
    }

    [Fact]
    public async Task Uppercase_Culture_Deletes_The_Canonically_Stored_Override()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_Update);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
        var key = AnyShippedKey();

        await SetTenantTextAsync(tenant.Id, Culture, key, "will be removed");

        var (response, _) = await client
            .DELETEAsync<TextDeleteEndpoint, TextDeleteRequest, EmptyResponse>(new() { Culture = Culture.ToUpperInvariant(), Key = key });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await DbContext.LocalizationTexts.AcrossAllTenants()
            .AnyAsync(x => x.TenantId == tenant.Id && x.Key == key, TestContext.Current.CancellationToken))
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
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);

        var (response, _) = await client
            .DELETEAsync<TextDeleteEndpoint, TextDeleteRequest, ProblemDetails>(new() { Culture = Culture, Key = AnyShippedKey() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
