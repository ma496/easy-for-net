namespace Backend.Tests.Features.Settings;

using System.Text.Json.Nodes;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Settings.Core;
using Backend.Features.Settings.Core.Entities;
using Backend.Tests.Features.Tenancy;
using Backend.Tests.Fakes;

/// <summary>
/// Base class for the settings suite. A tenant's own overrides are isolated by giving every test a
/// fresh tenant, but a platform row is read by every tenant - so every test that writes one shares this
/// class's collection, and every platform row is removed after each test so the next one starts from the
/// code defaults.
/// </summary>
/// <remarks>
/// The platform rows written here are of <see cref="ProbeSettings"/>, which exists only in the test host
/// and which nothing in the application reads, so they cannot change what another class's requests do.
/// No test writes the platform <c>Signin</c> row: the accounts the suite creates are unverified, and a
/// platform row requiring verification would refuse sign-ins across the whole suite while it stood. What
/// a platform <c>Signin</c> override does to a tenant is shown through <see cref="PlatformSettingOverlays"/>
/// instead, which applies to tenants the test created and nowhere else.
/// </remarks>
[Collection("Settings")]
public abstract class SettingsTestsBase(App app) : TenancyTestsBase(app)
{
    /// <summary>The probe setting's registered name.</summary>
    protected const string Probe = ProbeSettingsProvider.Name;

    /// <summary>Creates a member of the tenant holding the settings permissions named - both, by default - and a client signed in as it there.</summary>
    protected async Task<HttpClient> TenantClientAsync(Guid tenantId, params string[] permissions)
    {
        var roleId = await CreateTenantRoleAsync(tenantId, permissions.Length > 0 ? permissions : [Allow.Settings_View, Allow.Settings_Update]);
        var member = await CreateTenantUserAsync(tenantId, roleId);
        return await ClientForAsync(member.Username, tenantId);
    }

    /// <summary>A client signed in as the seeded platform administrator, acting in no tenant.</summary>
    protected async Task<HttpClient> PlatformClientAsync()
    {
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        await TestsHelper.SetNewAuthTokenAsync(client, TestUsers.PlatformAdminUsername, TestUsers.AdminPassword);
        return client;
    }

    /// <summary>The stored overrides of one setting in one scope, or <see langword="null"/> when that scope has no row.</summary>
    protected async Task<JsonObject?> StoredValuesAsync(string name, Guid? tenantId)
    {
        var values = await DbContext.SettingValues
            .AsNoTracking()
            .AcrossAllTenants()
            .Where(x => x.Name == name && x.TenantId == tenantId)
            .Select(x => x.Values)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        return values is null ? null : JsonNode.Parse(values)!.AsObject();
    }

    /// <summary>Writes the platform's overrides of a setting directly, standing in for a platform administrator's save.</summary>
    protected async Task SetPlatformValuesAsync(string name, JsonObject values)
    {
        using var platformScope = TenantContext.BeginPlatformScope();
        DbContext.SettingValues.Add(new SettingValue { Name = name, Values = values.ToJsonString() });
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Writes a tenant's overrides of a setting directly, standing in for its administrator's save.</summary>
    protected async Task SetTenantValuesAsync(Guid tenantId, string name, JsonObject values)
    {
        using var tenantScope = TenantContext.BeginTenant(tenantId);
        DbContext.SettingValues.Add(new SettingValue { Name = name, Values = values.ToJsonString() });
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Marks an account's email as verified, so sign-in never refuses it whatever the settings say.</summary>
    protected async Task MarkEmailVerifiedAsync(User user)
    {
        var account = await DbContext.Users.SingleAsync(candidate => candidate.Id == user.Id, TestContext.Current.CancellationToken);
        account.IsEmailVerified = true;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>One property of one setting in a listing or an update's answer.</summary>
    protected static SettingPropertyDto PropertyOf(SettingDto setting, string property)
        => setting.Properties.Single(x => x.Name == property);

    /// <summary>One property of one setting in a <c>GET /settings</c> listing.</summary>
    protected static SettingPropertyDto PropertyOf(Backend.Features.Settings.Endpoints.Settings.SettingListResponse list, string setting, string property)
        => PropertyOf(list.Items.Single(x => x.Name == setting), property);

    /// <summary>
    /// Removes every platform row this test may have written - directly or through an endpoint - so the
    /// next test in the collection starts from the code defaults.
    /// </summary>
    protected override async ValueTask TearDownAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var platformRows = await DbContext.SettingValues.AcrossAllTenants()
            .Where(x => x.TenantId == null)
            .ToListAsync(cancellationToken);
        if (platformRows.Count > 0)
        {
            DbContext.SettingValues.RemoveRange(platformRows);
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        await base.TearDownAsync();
    }
}