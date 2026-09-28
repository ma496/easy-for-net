namespace Backend.Tests.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests for <see cref="LanguagesDeleteEndpoint"/>: removing the acting scope's own row so it reverts
/// to what it inherits, idempotency, and the permission it requires.
/// </summary>
public class LanguagesDeleteTests(App app) : LocalizationTestsBase(app)
{
    [Fact]
    public async Task Delete_Reverts_The_Acting_Scope_To_Inherited()
    {
        var second = RequireSecondCulture();
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_Update, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);

        var (putResponse, _) = await client.PUTAsync<LanguagesUpdateEndpoint, LanguagesUpdateRequest, EmptyResponse>(
            new() { EnabledCultures = ["en", second], DefaultCulture = second });
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (response, _) = await client.DELETEAsync<LanguagesDeleteEndpoint, EmptyResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await DbContext.LanguageSettings.AcrossAllTenants()
            .AnyAsync(x => x.TenantId == tenant.Id, TestContext.Current.CancellationToken))
            .Should().BeFalse();

        var (_, result) = await client.GETAsync<LanguagesGetEndpoint, LanguagesGetResponse>();
        result.IsInherited.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_With_No_Row_Is_Idempotent()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_Update);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);

        var (response, _) = await client.DELETEAsync<LanguagesDeleteEndpoint, EmptyResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, "a scope with nothing of its own to remove still answers success");
    }

    [Fact]
    public async Task Missing_Permission_Is_Forbidden()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);

        var (response, _) = await client.DELETEAsync<LanguagesDeleteEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unauthenticated_Caller_Is_Refused()
    {
        var (response, _) = await Client.DELETEAsync<LanguagesDeleteEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
