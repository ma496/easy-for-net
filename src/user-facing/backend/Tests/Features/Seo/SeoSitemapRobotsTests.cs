using System.Text.Json.Nodes;
using Backend.Features.Localization.Core;
using Backend.Features.Seo.Endpoints.Seo;

namespace Backend.Tests.Features.Seo;

/// <summary>
/// Tests for <see cref="SeoSitemapEndpoint"/> and <see cref="SeoRobotsEndpoint"/>: which paths the
/// sitemap lists, what it says about them, the languages it names, and the indexing switch.
/// </summary>
public class SeoSitemapRobotsTests(App app) : SeoTestsBase(app)
{
    [Fact]
    public async Task Lists_Included_Indexable_Paths_Only()
    {
        var listed = NewPath();
        var noIndex = NewPath();
        var excluded = NewPath();
        var template = $"{NewPath()}/{{slug}}";
        await CreatePageAsync(new() { Path = listed, ChangeFrequency = "weekly", Priority = 0.8m });
        await CreatePageAsync(new() { Path = noIndex, NoIndex = true });
        await CreatePageAsync(new() { Path = excluded, IncludeInSitemap = false });
        await CreatePageAsync(new() { Path = template });

        var sitemap = await SitemapAsync();

        var entry = sitemap.Entries.Should().ContainSingle(e => e.Path == listed).Subject;
        entry.ChangeFrequency.Should().Be("weekly");
        entry.Priority.Should().Be(0.8m);
        entry.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        sitemap.Entries.Should().NotContain(e => e.Path == noIndex, "a noindex page is not offered to a crawler");
        sitemap.Entries.Should().NotContain(e => e.Path == excluded, "a page kept out of the sitemap is kept out");
        sitemap.Entries.Should().NotContain(e => e.Path == template, "a template names no address a crawler can fetch");
    }

    [Fact]
    public async Task A_Path_With_Several_Cultures_Is_Listed_Once()
    {
        var path = NewPath();
        await CreatePageAsync(new() { Path = path, Priority = 0.5m });
        await CreatePageAsync(new() { Path = path, Culture = "en", Priority = 0.9m });

        var entry = (await SitemapAsync()).Entries.Should().ContainSingle(e => e.Path == path).Subject;
        entry.Priority.Should().Be(0.5m, "the culture-neutral page speaks for the path");
    }

    [Fact]
    public async Task Names_The_Site_And_Its_Languages()
    {
        var sitemap = await SitemapAsync();

        sitemap.SiteUrl.Should().NotBeNullOrWhiteSpace().And.NotEndWith("/");
        sitemap.Cultures.Should().NotBeEmpty().And.BeSubsetOf(Service<ICultureCatalog>().ShippedCultures);
        sitemap.Cultures.Should().Contain(sitemap.DefaultCulture);
    }

    [Fact]
    public async Task Indexing_Turned_Off_Empties_The_Sitemap_And_Says_So_To_Robots()
    {
        await CreatePageAsync(new() { Path = NewPath() });
        await SetSeoAsync(new JsonObject { ["allowIndexing"] = false, ["robotsExtra"] = "Disallow: /private" });

        (await SitemapAsync()).Entries.Should().BeEmpty();

        var (response, robots) = await AnonymousClient().GETAsync<SeoRobotsEndpoint, SeoRobotsResponse>();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        robots.AllowIndexing.Should().BeFalse();
        robots.Extra.Should().Be("Disallow: /private");
        robots.SiteUrl.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Robots_Allows_Indexing_By_Default()
    {
        var (response, robots) = await AnonymousClient().GETAsync<SeoRobotsEndpoint, SeoRobotsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        robots.AllowIndexing.Should().BeTrue();
        robots.Extra.Should().BeEmpty();
    }
}
