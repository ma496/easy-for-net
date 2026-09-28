namespace Backend.Tests.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests for <see cref="TextListEndpoint"/>: row shape and inheritance, the <c>onlyOverridden</c> and
/// <c>filter</c> narrowing, pagination, validation, and the permission it requires.
/// </summary>
public class TextListTests(App app) : LocalizationTestsBase(app)
{
    [Fact]
    public async Task Row_Reports_Its_Own_Override_And_What_It_Inherits()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
        var key = AnyShippedKey();

        await SetPlatformTextAsync(Culture, key, "platform value");
        await SetTenantTextAsync(tenant.Id, Culture, key, "tenant value");

        var (response, page) = await client
            .GETAsync<TextListEndpoint, TextListRequest, TextListResponse>(new() { Culture = Culture, PageSize = 100 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var row = page.Items.Should().ContainSingle(i => i.Key == key).Which;
        row.Value.Should().Be("tenant value");
        row.InheritedValue.Should().Be("platform value",
            "without the tenant's own override, the platform's is what this scope would fall back to");
    }

    [Fact]
    public async Task Row_With_No_Override_Reports_A_Null_Value_And_The_Shipped_Default_As_Inherited()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
        var key = AnyShippedKey();
        var shipped = ResourceStore.EnglishResources[key];

        var (_, page) = await client
            .GETAsync<TextListEndpoint, TextListRequest, TextListResponse>(new() { Culture = Culture, PageSize = 100 });

        var row = page.Items.Should().ContainSingle(i => i.Key == key).Which;
        row.Value.Should().BeNull();
        row.DefaultValue.Should().Be(shipped);
        row.InheritedValue.Should().Be(shipped);
    }

    [Fact]
    public async Task Uppercase_Culture_Finds_The_Canonically_Stored_Override()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
        var key = AnyShippedKey();

        await SetTenantTextAsync(tenant.Id, Culture, key, "stored lowercase");

        var (response, page) = await client.GETAsync<TextListEndpoint, TextListRequest, TextListResponse>(
            new() { Culture = Culture.ToUpperInvariant(), PageSize = 100 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Items.Should().ContainSingle(i => i.Key == key).Which.Value.Should().Be("stored lowercase",
            "an uppercase query culture is mapped to the same canonical culture the override was stored under");
    }

    [Fact]
    public async Task Only_Overridden_Excludes_Untouched_Keys()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Localization_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var client = await ClientForAsync(member.Username, tenant.Id);
        var key = AnyShippedKey();

        await SetTenantTextAsync(tenant.Id, Culture, key, "overridden");

        var (_, page) = await client
            .GETAsync<TextListEndpoint, TextListRequest, TextListResponse>(
                new() { Culture = Culture, OnlyOverridden = true, PageSize = 100 });

        page.Items.Should().OnlyContain(i => i.Value != null);
        page.Items.Should().Contain(i => i.Key == key);
    }

    [Fact]
    public async Task Filter_Matches_The_Key_Case_Insensitively()
    {
        await SetAuthTokenAsync();
        var key = AnyShippedKey();

        var (_, page) = await Client
            .GETAsync<TextListEndpoint, TextListRequest, TextListResponse>(
                new() { Culture = Culture, Filter = key.ToUpperInvariant(), PageSize = 100 });

        page.Items.Should().Contain(i => i.Key == key);
    }

    [Fact]
    public async Task Pagination_Splits_The_Shipped_Keys_Across_Pages()
    {
        await SetAuthTokenAsync();

        var (_, firstPage) = await Client
            .GETAsync<TextListEndpoint, TextListRequest, TextListResponse>(new() { Culture = Culture, Page = 1, PageSize = 5 });
        var (_, secondPage) = await Client
            .GETAsync<TextListEndpoint, TextListRequest, TextListResponse>(new() { Culture = Culture, Page = 2, PageSize = 5 });

        firstPage.Items.Should().HaveCount(5);
        firstPage.Total.Should().Be(secondPage.Total);
        firstPage.Items.Select(i => i.Key).Should().NotIntersectWith(secondPage.Items.Select(i => i.Key));
    }

    [Fact]
    public async Task Unshipped_Culture_Is_Rejected()
    {
        await SetAuthTokenAsync();

        var (response, refusal) = await Client
            .GETAsync<TextListEndpoint, TextListRequest, ProblemDetails>(new() { Culture = "not-a-real-culture" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().Contain(e => e.Name.Equals("culture", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Missing_Permission_Is_Forbidden()
    {
        var tenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var client = await ClientForAsync(member.Username, tenant.Id);

        var (response, _) = await client
            .GETAsync<TextListEndpoint, TextListRequest, ProblemDetails>(new() { Culture = Culture });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unauthenticated_Caller_Is_Refused()
    {
        var (response, _) = await Client
            .GETAsync<TextListEndpoint, TextListRequest, ProblemDetails>(new() { Culture = Culture });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
