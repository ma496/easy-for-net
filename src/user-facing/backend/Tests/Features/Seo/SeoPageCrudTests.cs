using System.Text.Json.Nodes;
using Backend.Features.Seo.Endpoints.Pages;
using Backend.Features.Settings.Endpoints.Settings;

namespace Backend.Tests.Features.Seo;

/// <summary>
/// Tests for the SEO page administration endpoints: create, read, update, list and delete, the
/// permissions they require, the one-page-per-path-and-culture rule, and the validation of every field.
/// </summary>
public class SeoPageCrudTests(App app) : SeoTestsBase(app)
{
    [Fact]
    public async Task Create_Get_Update_List_Delete()
    {
        var admin = await AdminClientAsync();
        var path = NewPath();

        var (created, page) = await admin.POSTAsync<SeoPageCreateEndpoint, SeoPageCreateRequest, SeoPageDto>(new()
        {
            Path = $"  {path}  ",
            Culture = "EN",
            Title = "  Title  ",
            Description = " ",
            TwitterCard = "summary",
            ChangeFrequency = "monthly",
            Priority = 0.3m,
            JsonLd = "[{\"@type\":\"Organization\"}]"
        });

        created.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Path.Should().Be(path, "the path is trimmed");
        page.Culture.Should().Be("en", "the culture is spelled the way it is shipped");
        page.Title.Should().Be("Title");
        page.Description.Should().BeNull("blank text is stored as no value");
        page.TwitterCard.Should().Be("summary");
        page.IncludeInSitemap.Should().BeTrue();

        var (got, read) = await admin.GETAsync<SeoPageGetEndpoint, SeoPageGetRequest, SeoPageDto>(new() { Id = page.Id });
        got.StatusCode.Should().Be(HttpStatusCode.OK);
        read.Should().BeEquivalentTo(page, options => options.Excluding(x => x.UpdatedAt).Excluding(x => x.CreatedAt));

        var (updated, afterUpdate) = await admin.PUTAsync<SeoPageUpdateEndpoint, SeoPageUpdateRequest, SeoPageDto>(new()
        {
            Id = page.Id,
            Path = path,
            Culture = "en",
            Title = "Updated",
            NoIndex = true,
            IncludeInSitemap = false
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        afterUpdate.Title.Should().Be("Updated");
        afterUpdate.JsonLd.Should().BeNull("an update replaces every writable field");
        afterUpdate.NoIndex.Should().BeTrue();

        var (listed, list) = await admin.GETAsync<SeoPageListEndpoint, SeoPageListRequest, SeoPageListResponse>(
            new() { Search = path, SortField = "path", PageSize = 10 });
        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        var row = list.Items.Should().ContainSingle().Subject;
        row.Id.Should().Be(page.Id);
        row.Title.Should().Be("Updated");
        row.NoIndex.Should().BeTrue();
        row.IncludeInSitemap.Should().BeFalse();
        list.Total.Should().Be(1);

        var (deleted, deleteResult) = await admin.DELETEAsync<SeoPageDeleteEndpoint, SeoPageDeleteRequest, SeoPageDeleteResponse>(new() { Id = page.Id });
        deleted.StatusCode.Should().Be(HttpStatusCode.OK);
        deleteResult.Success.Should().BeTrue();

        var (missing, problem) = await admin.GETAsync<SeoPageGetEndpoint, SeoPageGetRequest, ProblemDetails>(new() { Id = page.Id });
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        problem.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SeoPageNotFound.Value);
    }

    [Fact]
    public async Task Search_Matches_The_Title_Too()
    {
        var marker = $"marker-{Guid.NewGuid():N}";
        await CreatePageAsync(new() { Path = NewPath(), Title = $"Title {marker}" });
        var admin = await AdminClientAsync();

        var (_, list) = await admin.GETAsync<SeoPageListEndpoint, SeoPageListRequest, SeoPageListResponse>(new() { Search = marker.ToUpperInvariant() });

        list.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Unsupported_Sort_Field_Is_Refused()
    {
        var admin = await AdminClientAsync();

        var (response, _) = await admin.GETAsync<SeoPageListEndpoint, SeoPageListRequest, ProblemDetails>(new() { SortField = "JsonLd" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anonymous_Caller_Is_Unauthorized()
    {
        var (list, _) = await AnonymousClient().GETAsync<SeoPageListEndpoint, SeoPageListRequest, ProblemDetails>(new());
        var (create, _) = await AnonymousClient().POSTAsync<SeoPageCreateEndpoint, SeoPageCreateRequest, ProblemDetails>(new() { Path = NewPath() });

        list.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        create.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Viewer_May_Read_But_Not_Write()
    {
        // The seeded limited account holds Seo.View and nothing else.
        var viewer = await ClientForAsync("limited");
        var page = await CreatePageAsync(new() { Path = NewPath() });

        (await viewer.GETAsync<SeoPageListEndpoint, SeoPageListRequest, SeoPageListResponse>(new())).Response.StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await viewer.GETAsync<SeoPageGetEndpoint, SeoPageGetRequest, SeoPageDto>(new() { Id = page.Id })).Response.StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await viewer.POSTAsync<SeoPageCreateEndpoint, SeoPageCreateRequest, ProblemDetails>(new() { Path = NewPath() })).Response.StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PUTAsync<SeoPageUpdateEndpoint, SeoPageUpdateRequest, ProblemDetails>(new() { Id = page.Id, Path = page.Path })).Response.StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await viewer.DELETEAsync<SeoPageDeleteEndpoint, SeoPageDeleteRequest, ProblemDetails>(new() { Id = page.Id })).Response.StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Caller_Without_Seo_View_Is_Forbidden()
    {
        var plain = await ClientForAsync("plain");

        var (response, _) = await plain.GETAsync<SeoPageListEndpoint, SeoPageListRequest, ProblemDetails>(new());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task One_Live_Page_Per_Path_And_Culture()
    {
        var admin = await AdminClientAsync();
        var path = NewPath();
        var neutral = await CreatePageAsync(new() { Path = path });

        var (duplicate, problem) = await admin.POSTAsync<SeoPageCreateEndpoint, SeoPageCreateRequest, ProblemDetails>(new() { Path = path });
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().Contain(e => e.Code == ErrorCodes.SeoPageAlreadyExists.Value && e.Name == "path");

        var english = await CreatePageAsync(new() { Path = path, Culture = "en" });
        english.Culture.Should().Be("en", "the same path in another culture is another page");

        var (moved, movedProblem) = await admin.PUTAsync<SeoPageUpdateEndpoint, SeoPageUpdateRequest, ProblemDetails>(
            new() { Id = english.Id, Path = path });
        moved.StatusCode.Should().Be(HttpStatusCode.BadRequest, "an update cannot move a page onto another live page's slot");
        movedProblem.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SeoPageAlreadyExists.Value);

        (await admin.DELETEAsync<SeoPageDeleteEndpoint, SeoPageDeleteRequest, SeoPageDeleteResponse>(new() { Id = neutral.Id }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await CreatePageAsync(new() { Path = path })).Path.Should().Be(path, "a deleted page frees its slot");
    }

    [Fact]
    public async Task Invalid_Json_Ld_Is_Refused_With_Its_Code()
    {
        var admin = await AdminClientAsync();

        foreach (var jsonLd in new[] { "{not json", "\"a string\"", "42" })
        {
            var (response, problem) = await admin.POSTAsync<SeoPageCreateEndpoint, SeoPageCreateRequest, ProblemDetails>(
                new() { Path = NewPath(), JsonLd = jsonLd });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"'{jsonLd}' is not a JSON object or array");
            problem.Errors.Should().Contain(e => e.Code == ErrorCodes.InvalidJsonLd.Value && e.Name == "jsonLd");
        }
    }

    [Theory]
    [InlineData("about")]
    [InlineData("/about/")]
    [InlineData("/a b")]
    [InlineData("/blog/{slug")]
    [InlineData("/a?b=1")]
    public async Task Malformed_Path_Is_Refused(string path)
    {
        var admin = await AdminClientAsync();

        var (response, problem) = await admin.POSTAsync<SeoPageCreateEndpoint, SeoPageCreateRequest, ProblemDetails>(new() { Path = path });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().Contain(e => e.Name == "path");
    }

    [Fact]
    public async Task Other_Fields_Are_Validated()
    {
        var admin = await AdminClientAsync();

        var (response, problem) = await admin.POSTAsync<SeoPageCreateEndpoint, SeoPageCreateRequest, ProblemDetails>(new()
        {
            Path = NewPath(),
            Culture = "xx",
            CanonicalUrl = "/relative",
            TwitterCard = "player",
            ChangeFrequency = "sometimes",
            Priority = 1.5m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(e => e.Name).Should().Contain(["culture", "canonicalUrl", "twitterCard", "changeFrequency", "priority"]);
    }

    [Fact]
    public async Task Missing_Page_Is_Not_Found_For_Update_And_Delete()
    {
        var admin = await AdminClientAsync();

        var (update, updateProblem) = await admin.PUTAsync<SeoPageUpdateEndpoint, SeoPageUpdateRequest, ProblemDetails>(
            new() { Id = Guid.NewGuid(), Path = NewPath() });
        var (delete, deleteProblem) = await admin.DELETEAsync<SeoPageDeleteEndpoint, SeoPageDeleteRequest, ProblemDetails>(
            new() { Id = Guid.NewGuid() });

        update.StatusCode.Should().Be(HttpStatusCode.NotFound);
        updateProblem.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SeoPageNotFound.Value);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        deleteProblem.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SeoPageNotFound.Value);
    }

    [Fact]
    public async Task Seo_Setting_Is_Validated()
    {
        var admin = await AdminClientAsync();

        var (response, problem) = await admin.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(new()
        {
            Name = "Seo",
            Values = new JsonObject
            {
                ["titleTemplate"] = "No placeholder",
                ["siteUrl"] = "ftp://example.com",
                ["defaultOgImageFileId"] = "not-a-guid"
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.SettingValueInvalid.Value);
        problem.Errors.Select(e => e.Name).Should().Contain(["values.titleTemplate", "values.siteUrl", "values.defaultOgImageFileId"]);
    }

    [Fact]
    public async Task Seo_Setting_Is_Listed_With_Its_Defaults()
    {
        var admin = await AdminClientAsync();

        var (_, list) = await admin.GETAsync<SettingListEndpoint, SettingListResponse>();

        var seo = list.Items.Single(item => item.Name == "Seo");
        PropertyOf(seo, "siteName").Value!.GetValue<string>().Should().Be("Easy For Net");
        PropertyOf(seo, "allowIndexing").Value!.GetValue<bool>().Should().BeTrue();
        seo.Properties.Should().OnlyContain(property => property.Source == "default");
    }

    [Fact]
    public async Task Seeded_Pages_Exist()
    {
        var seeded = await DbContext.SeoPages.AsNoTracking()
            .Where(page => page.Culture == null && (page.Path == "/" || page.Path == "/about" || page.Path == "/contact"))
            .Select(page => page.Path)
            .ToListAsync(TestContext.Current.CancellationToken);

        seeded.Should().BeEquivalentTo(["/", "/about", "/contact"]);
    }
}
