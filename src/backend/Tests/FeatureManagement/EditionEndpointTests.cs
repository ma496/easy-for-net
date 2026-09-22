namespace Backend.Tests.FeatureManagement;

using Backend.Features.Tenancy.Endpoints.Editions;

/// <summary>
/// Tests for the edition surface - the plans the platform sells.
/// </summary>
/// <remarks>
/// Every assertion is about a plan this test made and searched for by its own unique name. Editions
/// are global rows with no tenant to narrow by, so a test that counted them all, or read "the first
/// row", would be racing every other test in the run.
/// </remarks>
public class EditionEndpointTests(App app) : FeatureTestsBase(app)
{
    [Fact]
    public async Task A_Plan_Is_Created_And_Found_By_Name()
    {
        await SetPlatformAdminAuthTokenAsync();
        var name = UniqueName();

        var (createResponse, created) = await Client
            .POSTAsync<EditionCreateEndpoint, EditionCreateRequest, EditionCreateResponse>(
                new() { Name = name, Description = "A plan", DisplayOrder = 3 });

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        created.Id.Should().NotBeEmpty();
        created.Name.Should().Be(name);

        var (listResponse, list) = await Client
            .GETAsync<EditionListEndpoint, EditionListRequest, EditionListResponse>(new() { Search = name, All = true });

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        list.Items.Should().ContainSingle(item => item.Id == created.Id);
        list.Items.Single(item => item.Id == created.Id).TenantCount.Should().Be(0);
    }

    [Fact]
    public async Task A_Plan_Name_Is_Taken_Whatever_Case_It_Was_Claimed_In()
    {
        await SetPlatformAdminAuthTokenAsync();
        var name = UniqueName();
        await CreateThroughTheApiAsync(name);

        var (response, problem) = await Client
            .POSTAsync<EditionCreateEndpoint, EditionCreateRequest, ProblemDetails>(
                new() { Name = name.ToUpperInvariant() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.EditionNameAlreadyExists);
        problem.Errors.First().Name.Should().Be("name", "the form has a field to correct");
    }

    [Fact]
    public async Task A_Plan_Keeping_Its_Own_Name_Is_Not_A_Clash()
    {
        await SetPlatformAdminAuthTokenAsync();
        var name = UniqueName();
        var created = await CreateThroughTheApiAsync(name);

        var (response, updated) = await Client
            .PUTAsync<EditionUpdateEndpoint, EditionUpdateRequest, EditionUpdateResponse>(
                new() { Id = created.Id, Name = name, Description = "Described later", DisplayOrder = 7 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Description.Should().Be("Described later");
        updated.DisplayOrder.Should().Be(7);
    }

    [Fact]
    public async Task Reading_A_Plan_Reports_How_Many_Tenants_Are_On_It()
    {
        var edition = await CreateEditionAsync();
        await CreateTenantOnEditionAsync(edition.Id);
        await SetPlatformAdminAuthTokenAsync();

        var (response, result) = await Client
            .GETAsync<EditionGetEndpoint, EditionGetRequest, EditionGetResponse>(new() { Id = edition.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.TenantCount.Should().Be(1);
    }

    [Fact]
    public async Task A_Plan_Nobody_Is_On_Is_Deleted_Along_With_Its_Entitlements()
    {
        var edition = await CreateEditionAsync();
        await SetForEditionAsync(edition.Id, FeatureNames.FileManagement_Enabled, "false");
        await SetPlatformAdminAuthTokenAsync();

        var (response, _) = await Client
            .DELETEAsync<EditionDeleteEndpoint, EditionDeleteRequest, EditionDeleteResponse>(new() { Id = edition.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await FeatureValueStore.GetAllAsync(FeatureValueProviderNames.Edition, edition.Id.ToString(),
                                             TestContext.Current.CancellationToken))
            .Should().BeEmpty("the values carry no foreign key, so nothing else would ever remove them");
    }

    [Fact]
    public async Task A_Plan_A_Tenant_Is_On_Cannot_Be_Deleted()
    {
        var edition = await CreateEditionAsync();
        var tenant = await CreateTenantOnEditionAsync(edition.Id);
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .DELETEAsync<EditionDeleteEndpoint, EditionDeleteRequest, ProblemDetails>(new() { Id = edition.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.First().Code.Should().Be(ErrorCodes.EditionInUse);

        // The refusal is what keeps the tenant from silently dropping to the declared defaults.
        (await ResolveForTenantAsync(tenant.Id)).Should().NotBeNull();
        (await DbContext.Editions.AsNoTracking()
            .AnyAsync(row => row.Id == edition.Id, TestContext.Current.CancellationToken))
            .Should().BeTrue();
    }

    [Fact]
    public async Task An_Unknown_Plan_Is_Not_Found()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, _) = await Client
            .GETAsync<EditionGetEndpoint, EditionGetRequest, EditionGetResponse>(new() { Id = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_Name_That_Breaks_The_Rules_Is_Refused_By_Field()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await Client
            .POSTAsync<EditionCreateEndpoint, EditionCreateRequest, ProblemDetails>(new() { Name = "a" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(error => error.Name).Should().Contain("name");
    }

    #region Helpers

    private static string UniqueName() => $"Plan {Guid.NewGuid():N}";

    private async Task<EditionCreateResponse> CreateThroughTheApiAsync(string name)
    {
        var (response, created) = await Client
            .POSTAsync<EditionCreateEndpoint, EditionCreateRequest, EditionCreateResponse>(new() { Name = name });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return created;
    }

    #endregion
}
