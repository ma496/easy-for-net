using Backend.Features.Localization.Endpoints.Localization;
using Backend.Tests.Fakes;

namespace Backend.Tests.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests that an anonymous <c>GET /localization/resources/{culture}</c> on a tenant's host serves that
/// tenant's overrides and language settings, the platform's on the platform host, on an unknown tenant's
/// host and where hosts name nothing, and that a signed-in caller still sees the tenant it acts in.
/// </summary>
/// <remarks>
/// In the <c>Localization</c> collection rather than <c>TenantHosting</c>: what these tests share with other
/// classes is the platform override row they write, which every scope reads through and which
/// <see cref="LocalizationTestsBase"/> removes after each test. Host resolution is turned on per request by
/// <see cref="TenantHostingOverride.GateHeader"/>, which no other class can see, so it needs no collection.
/// </remarks>
public class ResourcesGetHostTests(App app) : LocalizationTestsBase(app)
{
    /// <summary>
    /// The tenant's override on its host, the platform's on the platform host, on an unknown tenant's host,
    /// and with resolution off.
    /// </summary>
    [Fact]
    public async Task Anonymous_Caller_Sees_The_Host_Tenants_Overrides()
    {
        var key = AnyShippedKey();
        var tenant = await CreateTenantAsync();
        var platformValue = $"platform-{Guid.NewGuid():N}";
        var tenantValue = $"tenant-{Guid.NewGuid():N}";
        await SetPlatformTextAsync(Culture, key, platformValue);
        await SetTenantTextAsync(tenant.Id, Culture, key, tenantValue);

        (await ResourceOnAsync(HostClient(TenantHostingOverride.TenantHost(tenant.Identifier)), key)).Should().Be(tenantValue);
        (await ResourceOnAsync(HostClient(TenantHostingOverride.PlatformHost), key)).Should().Be(platformValue);
        (await ResourceOnAsync(HostClient(TenantHostingOverride.TenantHost($"t-{Guid.NewGuid():N}")), key)).Should().Be(platformValue,
            "a host naming no existing tenant falls back to the platform");
        (await ResourceOnAsync(App.CreateClient(new ClientOptions { HandleCookies = false }), key)).Should().Be(platformValue,
            "with resolution off an anonymous caller sees the platform's overrides only");
    }

    /// <summary>The host tenant's language settings decide the default an anonymous visitor is served.</summary>
    [Fact]
    public async Task Anonymous_Caller_Sees_The_Host_Tenants_Languages()
    {
        var tenant = await CreateTenantAsync();
        await SetTenantLanguagesAsync(tenant.Id, [Culture], Culture);

        using var onTenant = HostClient(TenantHostingOverride.TenantHost(tenant.Identifier));
        var (response, resources) = await onTenant.GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = Culture });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        resources.DefaultCulture.Should().Be(Culture);
        resources.Languages.Select(language => language.Code).Should().Equal(Culture);

        using var onPlatform = HostClient(TenantHostingOverride.PlatformHost);
        var (_, platform) = await onPlatform.GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = Culture });
        platform.DefaultCulture.Should().BeNull("the platform has no language settings row of its own here");
    }

    /// <summary>A signed-in caller on a host naming nothing still sees the tenant it acts in, as before.</summary>
    [Fact]
    public async Task Signed_In_Caller_Sees_Its_Own_Tenant()
    {
        var key = AnyShippedKey();
        var tenant = await CreateTenantAsync();
        var tenantValue = $"tenant-{Guid.NewGuid():N}";
        await SetTenantTextAsync(tenant.Id, Culture, key, tenantValue);
        var member = await CreateTenantUserAsync(tenant.Id);

        var client = await ClientForAsync(member.Username, tenant.Id);

        (await ResourceOnAsync(client, key)).Should().Be(tenantValue);
    }

    private HttpClient HostClient(string host)
    {
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Host = host;
        client.DefaultRequestHeaders.Add(TenantHostingOverride.GateHeader, "on");
        return client;
    }

    private static async Task<string> ResourceOnAsync(HttpClient client, string key)
    {
        using (client)
        {
            var (response, resources) = await client.GETAsync<ResourcesGetEndpoint, ResourcesGetRequest, ResourcesGetResponse>(new() { Culture = Culture });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return resources.Resources[key];
        }
    }
}
