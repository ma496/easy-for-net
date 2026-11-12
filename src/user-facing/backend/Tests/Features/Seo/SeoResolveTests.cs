using System.Text.Json.Nodes;
using Backend.Features.Seo.Endpoints.Seo;
using Backend.Settings;
using Microsoft.Extensions.Options;

namespace Backend.Tests.Features.Seo;

/// <summary>
/// Tests for <see cref="SeoResolveEndpoint"/>: every field merged from the culture's page, then the
/// culture-neutral page, then the <c>Seo</c> setting; templates standing for every path of their shape;
/// absolute URLs on the site's origin; and the indexing switch.
/// </summary>
public class SeoResolveTests(App app) : SeoTestsBase(app)
{
    private string DefaultDomain => Service<IOptions<WebSetting>>().Value.DefaultDomain.TrimEnd('/');

    [Fact]
    public async Task Culture_Page_Wins_And_The_Neutral_Page_Fills_What_It_Leaves_Out()
    {
        var path = NewPath();
        await CreatePageAsync(new() { Path = path, Title = "Neutral title", Description = "Neutral description", Keywords = "neutral" });
        await CreatePageAsync(new() { Path = path, Culture = "en", Title = "English title", NoFollow = true });

        var resolved = await ResolveAsync(path, "en");

        resolved.Title.Should().Be("English title", "the culture's own page answers first");
        resolved.Description.Should().Be("Neutral description", "a field the culture's page leaves out comes from the neutral page");
        resolved.Keywords.Should().Be("neutral");
        resolved.Follow.Should().BeFalse("the switches are the most specific page's");
        resolved.Index.Should().BeTrue();
    }

    [Fact]
    public async Task Neutral_Page_Answers_For_A_Culture_That_Has_None()
    {
        var path = NewPath();
        await CreatePageAsync(new() { Path = path, Title = "Neutral title" });

        (await ResolveAsync(path, "en")).Title.Should().Be("Neutral title");
        (await ResolveAsync(path)).Title.Should().Be("Neutral title");
    }

    [Fact]
    public async Task Unknown_Path_Answers_The_Defaults()
    {
        await SetSeoAsync(new JsonObject { ["defaultDescription"] = "Site description", ["twitterSite"] = "@site" });

        var resolved = await ResolveAsync(NewPath());

        resolved.Title.Should().BeNull("only a page's own title is answered; the web app applies the template");
        resolved.TitleTemplate.Should().Be("%s | Easy For Net");
        resolved.SiteName.Should().Be("Easy For Net");
        resolved.SiteUrl.Should().Be(DefaultDomain, "an empty SiteUrl falls back to Web:DefaultDomain");
        resolved.Description.Should().Be("Site description");
        resolved.TwitterSite.Should().Be("@site");
        resolved.TwitterCard.Should().Be("summary_large_image");
        resolved.CanonicalUrl.Should().BeNull();
        resolved.OgImageUrl.Should().BeNull();
        resolved.Index.Should().BeTrue();
        resolved.Follow.Should().BeTrue();
    }

    [Fact]
    public async Task Template_Stands_For_Every_Path_Of_Its_Shape()
    {
        var root = NewPath();
        await CreatePageAsync(new() { Path = $"{root}/{{slug}}", Title = "A post", JsonLd = "{\"@type\":\"BlogPosting\"}" });

        var resolved = await ResolveAsync($"{root}/my-first-post");

        resolved.Title.Should().Be("A post");
        resolved.JsonLd.Should().Be("{\"@type\":\"BlogPosting\"}");
        (await ResolveAsync($"{root}/a/b")).Title.Should().BeNull("a placeholder stands for one segment only");
    }

    [Fact]
    public async Task Exact_Page_Wins_Over_A_Matching_Template()
    {
        var root = NewPath();
        await CreatePageAsync(new() { Path = $"{root}/{{slug}}", Title = "Template" });
        await CreatePageAsync(new() { Path = $"{root}/special", Title = "Exact" });

        (await ResolveAsync($"{root}/special")).Title.Should().Be("Exact");
        (await ResolveAsync($"{root}/other")).Title.Should().Be("Template");
    }

    [Fact]
    public async Task Urls_Are_Absolute_On_The_Configured_Site_Url()
    {
        var imageId = Guid.NewGuid();
        await SetSeoAsync(new JsonObject { ["siteUrl"] = "https://www.example.com/", ["defaultOgImageFileId"] = imageId.ToString() });
        var path = NewPath();
        await CreatePageAsync(new() { Path = path, CanonicalUrl = "https://www.example.com/canonical" });

        var resolved = await ResolveAsync(path);

        resolved.SiteUrl.Should().Be("https://www.example.com", "the trailing slash is dropped");
        resolved.CanonicalUrl.Should().Be("https://www.example.com/canonical");
        resolved.OgImageUrl.Should().Be($"https://www.example.com/api/seo/images/{imageId:D}", "with no page image the default one is served");
    }

    [Fact]
    public async Task Page_Image_Wins_Over_The_Default_Image()
    {
        var pageImage = Guid.NewGuid();
        await SetSeoAsync(new JsonObject { ["defaultOgImageFileId"] = Guid.NewGuid().ToString() });
        var path = NewPath();
        await CreatePageAsync(new() { Path = path, OgImageFileId = pageImage });

        (await ResolveAsync(path)).OgImageUrl.Should().EndWith($"/seo/images/{pageImage:D}");
    }

    [Fact]
    public async Task NoIndex_Page_Is_Not_Indexed()
    {
        var path = NewPath();
        await CreatePageAsync(new() { Path = path, NoIndex = true });

        (await ResolveAsync(path)).Index.Should().BeFalse();
    }

    [Fact]
    public async Task Indexing_Turned_Off_Marks_Every_Page_NoIndex()
    {
        var path = NewPath();
        await CreatePageAsync(new() { Path = path, Title = "Indexed page" });
        (await ResolveAsync(path)).Index.Should().BeTrue("the premise is a page that would be indexed");

        await SetSeoAsync(new JsonObject { ["allowIndexing"] = false });

        var resolved = await ResolveAsync(path);
        resolved.Index.Should().BeFalse("the switch is a staging deployment's, and outranks every page");
        resolved.Title.Should().Be("Indexed page", "the cached answer was dropped when the setting changed, and nothing else moved");
    }

    [Fact]
    public async Task A_Page_Write_Reaches_The_Next_Resolve()
    {
        var path = NewPath();
        var page = await CreatePageAsync(new() { Path = path, Title = "Before" });
        (await ResolveAsync(path)).Title.Should().Be("Before");

        var admin = await AdminClientAsync();
        (await admin.PUTAsync<Backend.Features.Seo.Endpoints.Pages.SeoPageUpdateEndpoint, Backend.Features.Seo.Endpoints.Pages.SeoPageUpdateRequest, Backend.Features.Seo.Endpoints.Pages.SeoPageDto>(
                new() { Id = page.Id, Path = path, Title = "After" }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ResolveAsync(path)).Title.Should().Be("After", "every page write drops the cache");
    }

    [Fact]
    public async Task Path_Must_Start_With_A_Slash()
    {
        var (response, _) = await AnonymousClient()
            .GETAsync<SeoResolveEndpoint, SeoResolveRequest, ProblemDetails>(new() { Path = "about" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
