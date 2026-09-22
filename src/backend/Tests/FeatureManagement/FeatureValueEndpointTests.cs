namespace Backend.Tests.FeatureManagement;

using Backend.Features.Tenancy.Endpoints.FeatureValues;

/// <summary>
/// Tests for the feature-management surface - reading and setting what a tenant or an edition is
/// entitled to, and the account's own read of what its plan includes.
/// </summary>
public class FeatureValueEndpointTests(App app) : FeatureTestsBase(app)
{
    [Fact]
    public async Task The_Catalogue_Comes_Back_With_Effective_Values_And_Where_They_Came_From()
    {
        var edition = await CreateEditionAsync();
        var tenant = await CreateTenantOnEditionAsync(edition.Id);
        await SetForEditionAsync(edition.Id, FeatureNames.Identity_MaxUserCount, "500");
        await SetPlatformAdminAuthTokenAsync();

        var feature = await FeatureRowAsync(FeatureValueProviderNames.Tenant, tenant.Id,
                                            FeatureNames.Identity_MaxUserCount);

        feature.Value.Should().Be("500");
        feature.ProviderName.Should().Be(FeatureValueProviderNames.Edition);
        feature.IsOverridden.Should().BeFalse("the tenant inherited it rather than setting it");
        feature.ValueType.Name.Should().Be("FreeText");
        feature.ValueType.ValidatorName.Should().Be("Numeric");
        feature.ValueType.ValidatorProperties.Should().ContainKey("maximum");
        feature.Depth.Should().Be(1, "a seat count refines the feature that allows provisioning at all");
        feature.ParentName.Should().Be(FeatureNames.Identity_UserManagement);
    }

    [Fact]
    public async Task Setting_A_Value_Marks_It_As_This_Providers_Own()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var response = await SetThroughTheApiAsync(FeatureValueProviderNames.Tenant, tenant.Id,
                                                   FeatureNames.FileManagement_Enabled, "false");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var feature = await FeatureRowAsync(FeatureValueProviderNames.Tenant, tenant.Id,
                                            FeatureNames.FileManagement_Enabled);
        feature.Value.Should().Be("false");
        feature.ProviderName.Should().Be(FeatureValueProviderNames.Tenant);
        feature.IsOverridden.Should().BeTrue();
        feature.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Clearing_A_Value_Puts_It_Back_To_What_It_Inherits()
    {
        var edition = await CreateEditionAsync();
        var tenant = await CreateTenantOnEditionAsync(edition.Id);
        await SetForEditionAsync(edition.Id, FeatureNames.Identity_MaxUserCount, "500");
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_MaxUserCount, "1000");
        await SetPlatformAdminAuthTokenAsync();

        await SetThroughTheApiAsync(FeatureValueProviderNames.Tenant, tenant.Id,
                                    FeatureNames.Identity_MaxUserCount, null);

        var feature = await FeatureRowAsync(FeatureValueProviderNames.Tenant, tenant.Id,
                                            FeatureNames.Identity_MaxUserCount);
        feature.Value.Should().Be("500");
        feature.ProviderName.Should().Be(FeatureValueProviderNames.Edition);
        feature.IsOverridden.Should().BeFalse();
    }

    [Fact]
    public async Task A_Value_The_Feature_Will_Not_Accept_Is_Refused_And_Nothing_Is_Stored()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .PUTAsync<FeatureValueUpdateEndpoint, FeatureValueUpdateRequest, ProblemDetails>(new()
            {
                ProviderName = FeatureValueProviderNames.Tenant,
                ProviderKey = tenant.Id.ToString(),
                Features = [new() { Name = FeatureNames.Identity_MaxUserCount, Value = "not a number" }]
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.First().Code.Should().Be(ErrorCodes.InvalidFeatureValue);
        (await FeatureValueStore.GetAllAsync(FeatureValueProviderNames.Tenant, tenant.Id.ToString(),
                                             TestContext.Current.CancellationToken))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task One_Bad_Value_Leaves_The_Whole_Payload_Unapplied()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, _) = await Client
            .PUTAsync<FeatureValueUpdateEndpoint, FeatureValueUpdateRequest, ProblemDetails>(new()
            {
                ProviderName = FeatureValueProviderNames.Tenant,
                ProviderKey = tenant.Id.ToString(),
                Features =
                [
                    new() { Name = FeatureNames.FileManagement_Enabled, Value = "false" },
                    new() { Name = FeatureNames.Identity_MaxUserCount, Value = "not a number" }
                ]
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await FeatureValueStore.GetAllAsync(FeatureValueProviderNames.Tenant, tenant.Id.ToString(),
                                             TestContext.Current.CancellationToken))
            .Should().BeEmpty("everything is checked before anything is written, so a bad row leaves the provider as it was");
    }

    [Fact]
    public async Task A_Feature_Nothing_Declares_Is_Refused()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .PUTAsync<FeatureValueUpdateEndpoint, FeatureValueUpdateRequest, ProblemDetails>(new()
            {
                ProviderName = FeatureValueProviderNames.Tenant,
                ProviderKey = tenant.Id.ToString(),
                Features = [new() { Name = "Nothing.Declares.This", Value = "true" }]
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.First().Code.Should().Be(ErrorCodes.FeatureNotFound);
    }

    [Fact]
    public async Task A_Provider_That_Stores_Nothing_Is_Refused()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, _) = await Client
            .PUTAsync<FeatureValueUpdateEndpoint, FeatureValueUpdateRequest, ProblemDetails>(new()
            {
                ProviderName = FeatureValueProviderNames.Default,
                ProviderKey = tenant.Id.ToString(),
                Features = [new() { Name = FeatureNames.FileManagement_Enabled, Value = "false" }]
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "configuration and the declared defaults are read-only fallbacks and belong to nobody");
    }

    [Fact]
    public async Task A_Provider_Key_Naming_Nothing_Is_Not_Found()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, _) = await Client
            .GETAsync<FeatureValueGetEndpoint, FeatureValueGetRequest, FeatureValueGetResponse>(new()
            {
                ProviderName = FeatureValueProviderNames.Tenant,
                ProviderKey = Guid.NewGuid().ToString()
            });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_Tenant_Administrator_Can_Read_Its_Own_Plan_Without_Any_Entitlement_Permission()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");
        var role = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var account = await CreateTenantUserAsync(tenant.Id, role);
        await SignInAsAsync(account.Username, tenant.Id);

        var (response, mine) = await Client.GETAsync<MyFeaturesEndpoint, MyFeaturesResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mine.Features[FeatureNames.FileManagement_Enabled].Should().Be("false");
        mine.Enabled[FeatureNames.FileManagement_Enabled].Should().BeFalse();
        mine.Enabled[FeatureNames.Identity_UserManagement].Should().BeTrue();
    }

    [Fact]
    public async Task A_Tenant_Administrator_Cannot_Write_Its_Own_Entitlements()
    {
        var tenant = await CreateTenantAsync();
        var role = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var account = await CreateTenantUserAsync(tenant.Id, role);
        await SignInAsAsync(account.Username, tenant.Id);

        var response = await SetThroughTheApiAsync(FeatureValueProviderNames.Tenant, tenant.Id,
                                                   FeatureNames.FileManagement_Enabled, "true");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a tenant able to write its own entitlements would simply switch on whatever its plan withholds");
    }

    #region Helpers

    private async Task<HttpResponseMessage> SetThroughTheApiAsync(string providerName,
                                                                  Guid providerKey,
                                                                  string feature,
                                                                  string? value)
    {
        var (response, _) = await Client
            .PUTAsync<FeatureValueUpdateEndpoint, FeatureValueUpdateRequest, FeatureValueUpdateResponse>(new()
            {
                ProviderName = providerName,
                ProviderKey = providerKey.ToString(),
                Features = [new() { Name = feature, Value = value }]
            });
        return response;
    }

    private async Task<FeatureDto> FeatureRowAsync(string providerName, Guid providerKey, string feature)
    {
        var (response, result) = await Client
            .GETAsync<FeatureValueGetEndpoint, FeatureValueGetRequest, FeatureValueGetResponse>(new()
            {
                ProviderName = providerName,
                ProviderKey = providerKey.ToString()
            });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return result.Groups.SelectMany(group => group.Features).Single(row => row.Name == feature);
    }

    #endregion
}
