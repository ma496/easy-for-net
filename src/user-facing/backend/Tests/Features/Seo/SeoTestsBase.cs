using System.Text.Json.Nodes;
using Backend.Features.Seo.Core;
using Backend.Features.Seo.Endpoints.Pages;
using Backend.Features.Seo.Endpoints.Seo;
using Backend.Features.Settings.Endpoints.Settings;
using Backend.Tests.Features.Settings;

namespace Backend.Tests.Features.Seo;

/// <summary>
/// Base class for the SEO suite. The public endpoints read the <c>Seo</c> setting and the pages through
/// one process-wide cache, so the suite shares the <c>Settings</c> collection with the other tests that
/// write a stored setting row, and drops the cache after each test - whose rows the base class removes
/// directly, behind the cache's back.
/// </summary>
/// <remarks>
/// Every page a test makes lives under a path of its own (<see cref="NewPath"/>), so the pages other
/// tests made never answer for it and assertions over the sitemap name only paths the test created.
/// </remarks>
public abstract class SeoTestsBase(App app) : SettingsTestsBase(app)
{
    /// <summary>A path no other test, and no earlier run, uses.</summary>
    protected static string NewPath(string? suffix = null) => $"/t-{Guid.NewGuid():N}{suffix}";

    /// <summary>A client with no credentials at all, as a crawler or a visitor is.</summary>
    protected HttpClient AnonymousClient() => App.CreateClient(new ClientOptions { HandleCookies = false });

    /// <summary>Creates a page through the endpoint as the administrator and returns what it answered.</summary>
    protected async Task<SeoPageDto> CreatePageAsync(SeoPageCreateRequest request)
    {
        var admin = await AdminClientAsync();
        var (response, page) = await admin.POSTAsync<SeoPageCreateEndpoint, SeoPageCreateRequest, SeoPageDto>(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "a test that arranges a page must actually have created it");
        return page;
    }

    /// <summary>Resolves a page's metadata anonymously.</summary>
    protected async Task<SeoResolveResponse> ResolveAsync(string path, string? culture = null)
    {
        var (response, resolved) = await AnonymousClient()
            .GETAsync<SeoResolveEndpoint, SeoResolveRequest, SeoResolveResponse>(new() { Path = path, Culture = culture });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return resolved;
    }

    /// <summary>Reads the sitemap anonymously.</summary>
    protected async Task<SeoSitemapResponse> SitemapAsync()
    {
        var (response, sitemap) = await AnonymousClient().GETAsync<SeoSitemapEndpoint, SeoSitemapResponse>();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return sitemap;
    }

    /// <summary>Saves the <c>Seo</c> setting's overrides through the endpoint, as an administrator would.</summary>
    protected async Task SetSeoAsync(JsonObject values)
    {
        var admin = await AdminClientAsync();
        var (response, _) = await admin.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, Backend.Features.Settings.Core.SettingDto>(
            new() { Name = SeoSettingsProvider.SeoSettingName, Values = values });
        response.StatusCode.Should().Be(HttpStatusCode.OK, "a test that arranges the Seo setting must actually have saved it");
    }

    /// <summary>Removes the stored rows, then the cache that may have been filled from them.</summary>
    protected override async ValueTask TearDownAsync()
    {
        await base.TearDownAsync();
        App.Services.GetRequiredService<ISeoCache>().Invalidate();
    }
}
