using System.Text.Json.Nodes;
using Backend.Features.Settings.Core;
using Backend.Features.Settings.Endpoints.Settings;

namespace Backend.Tests.Features.Settings;

/// <summary>
/// Tests for a platform-only setting property (<see cref="PlatformSettingAttribute"/>), on the one the
/// application declares - <c>General.IsSignupEnabled</c>: a tenant may neither set it nor have it
/// resolve from its own row, and the platform may set it.
/// </summary>
/// <remarks>
/// The platform write here stores the property's default (<see langword="false"/>), never
/// <see langword="true"/>: a platform row opening sign-up would reach every test class running beside
/// this one, as <see cref="SettingsTestsBase"/> explains for the <c>General</c> row as a whole.
/// </remarks>
public class PlatformSettingTests(App app) : SettingsTestsBase(app)
{
    private const string SignupEnabled = "isSignupEnabled";

    /// <summary>A tenant naming a platform-only property is refused with 400 <c>settingPropertyPlatformOnly</c>, and nothing is stored.</summary>
    [Fact]
    public async Task Tenant_Write_Naming_A_Platform_Only_Property_Is_Refused()
    {
        var tenant = await CreateTenantAsync();
        var client = await TenantClientAsync(tenant.Id);

        var (response, refusal) = await client.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, ProblemDetails>(
            new() { Name = GeneralSettingsProvider.GeneralSettingName, Values = new JsonObject { [SignupEnabled] = true, ["isEmailVerificationRequired"] = false } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().ContainSingle()
            .Which.Should().Match<ProblemDetails.Error>(error =>
                error.Code == ErrorCodes.SettingPropertyPlatformOnly.Value && error.Name == $"values.{SignupEnabled}");
        (await StoredValuesAsync(GeneralSettingsProvider.GeneralSettingName, tenant.Id)).Should().BeNull("a refused write stores nothing");
    }

    /// <summary>
    /// A tenant row holding a platform-only property - written by hand, or before the property was marked -
    /// has no effect: the tenant resolves the platform's value, and the property's source is never <c>tenant</c>.
    /// </summary>
    [Fact]
    public async Task Tenant_Row_Holding_A_Platform_Only_Property_Is_Ignored()
    {
        var tenant = await CreateTenantAsync();
        await SetTenantValuesAsync(tenant.Id, GeneralSettingsProvider.GeneralSettingName, new JsonObject { [SignupEnabled] = true });

        var resolved = await Service<ISettingProvider>().GetAsync<GeneralSettings>(tenant.Id, TestContext.Current.CancellationToken);
        resolved.IsSignupEnabled.Should().BeFalse("the tenant layer is skipped for a platform-only property");

        var client = await TenantClientAsync(tenant.Id);
        var (response, list) = await client.GETAsync<SettingListEndpoint, SettingListResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var property = PropertyOf(list, GeneralSettingsProvider.GeneralSettingName, SignupEnabled);
        property.Value!.GetValue<bool>().Should().BeFalse();
        property.Source.Should().NotBe("tenant");
        property.IsPlatformOnly.Should().BeTrue();
        PropertyOf(list, GeneralSettingsProvider.GeneralSettingName, "isEmailVerificationRequired").IsPlatformOnly.Should().BeFalse();
    }

    /// <summary>The platform may set a platform-only property; it is stored in the platform row and reported from it.</summary>
    [Fact]
    public async Task Platform_Write_Of_A_Platform_Only_Property_Succeeds()
    {
        var platform = await PlatformClientAsync();

        var (response, updated) = await platform.PUTAsync<SettingUpdateEndpoint, SettingUpdateRequest, SettingDto>(
            new() { Name = GeneralSettingsProvider.GeneralSettingName, Values = new JsonObject { [SignupEnabled] = false } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PropertyOf(updated, SignupEnabled).Source.Should().Be("platform");
        PropertyOf(updated, SignupEnabled).IsPlatformOnly.Should().BeTrue();
        (await StoredValuesAsync(GeneralSettingsProvider.GeneralSettingName, null))![SignupEnabled]!.GetValue<bool>().Should().BeFalse();
    }
}
