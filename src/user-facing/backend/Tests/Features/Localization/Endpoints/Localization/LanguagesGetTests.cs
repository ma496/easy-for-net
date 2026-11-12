using Backend.Features.Localization.Core.Entities;
using Backend.Features.Localization.Endpoints.Localization;

namespace Backend.Tests.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests for <see cref="LanguagesGetEndpoint"/>: row-level inheritance between a tenant's own settings,
/// the platform's, and the shipped defaults, and the permission it requires.
/// </summary>
public class LanguagesGetTests(App app) : LocalizationTestsBase(app)
{
    [Fact]
    public async Task With_No_Row_Anywhere_Every_Shipped_Culture_Is_Enabled_With_No_Default()
    {
        var roleId = await CreateRoleAsync(Allow.Localization_View);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);
        var shippedCultures = ResourceStore.ShippedCultures;

        var (response, result) = await client.GETAsync<LanguagesGetEndpoint, LanguagesGetResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.IsInherited.Should().BeTrue();
        result.EnabledCultures.Should().BeEquivalentTo(shippedCultures);
        result.DefaultCulture.Should().BeNull();
        result.InheritedEnabledCultures.Should().BeEquivalentTo(shippedCultures);
        result.InheritedDefaultCulture.Should().BeNull();
        result.Languages.Select(l => l.Code).Should().BeEquivalentTo(shippedCultures,
            "this response lists every shipped culture, not only the enabled ones");
    }

    [Fact]
    public async Task Own_Row_Drops_A_Stored_Culture_That_Is_No_Longer_Shipped()
    {
        var second = RequireSecondCulture();
        var roleId = await CreateRoleAsync(Allow.Localization_View);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);

        await SetLanguagesAsync(["en", second, UnshippedCulture], UnshippedCulture);

        var (_, result) = await client.GETAsync<LanguagesGetEndpoint, LanguagesGetResponse>();

        result.IsInherited.Should().BeFalse();
        result.EnabledCultures.Should().BeEquivalentTo(["en", second],
            "a culture the resource files no longer ship is dropped from a stored row");
        result.DefaultCulture.Should().BeNull("the stored default is itself unshipped, so it does not survive the intersection");
    }

    [Fact]
    public async Task Own_Row_With_Nothing_Left_Shipped_Falls_Back_To_Inherited()
    {
        var roleId = await CreateRoleAsync(Allow.Localization_View);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);
        var shippedCultures = ResourceStore.ShippedCultures;

        await SetLanguagesAsync([UnshippedCulture]);

        var (_, result) = await client.GETAsync<LanguagesGetEndpoint, LanguagesGetResponse>();

        result.IsInherited.Should().BeTrue("a row with nothing shipped left counts as no row at all");
        result.EnabledCultures.Should().BeEquivalentTo(shippedCultures);
    }

    [Fact]
    public async Task Missing_Permission_Is_Forbidden()
    {
        var member = await CreateUserAsync();
        var client = await ClientForAsync(member.Username);

        var (response, _) = await client.GETAsync<LanguagesGetEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unauthenticated_Caller_Is_Refused()
    {
        var (response, _) = await Client.GETAsync<LanguagesGetEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
