using System.Text.Json.Nodes;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Settings.Core;
using Backend.Features.Settings.Core.Entities;
using Backend.Tests.Fakes;

namespace Backend.Tests.Features.Settings;

/// <summary>
/// Base class for the settings suite. A stored row is read by every request, so every test that writes
/// one shares this class's collection, and every row is removed after each test so the next one starts
/// from the defaults.
/// </summary>
/// <remarks>
/// The rows written here are of <see cref="ProbeSettings"/>, which exists only in the test host and which
/// nothing in the application reads, of <c>Email</c>, which only a sending email job reads - and no
/// Hangfire worker runs under Testing, so the only jobs that ever send are the ones this suite runs
/// itself - and of <c>Seo</c>, which only the public SEO endpoints read. None can change what another
/// class's requests do. No test writes a <c>General</c> row that changes behaviour: the accounts the
/// suite creates are unverified, so a row requiring verification would refuse sign-ins across the whole
/// suite while it stood. What a <c>General</c> override does is shown through <see cref="SettingOverlays"/>
/// instead, which applies to the requests of one client and nowhere else.
/// </remarks>
[Collection("Settings")]
public abstract class SettingsTestsBase(App app) : UsersTestsBase(app)
{
    /// <summary>The probe setting's registered name.</summary>
    protected const string Probe = ProbeSettingsProvider.Name;

    /// <summary>Creates an account holding the settings permissions named - both, by default - and a client signed in as it.</summary>
    protected async Task<HttpClient> SettingsClientAsync(params string[] permissions)
    {
        var roleId = await CreateRoleAsync(permissions.Length > 0 ? permissions : [Allow.Settings_View, Allow.Settings_Update]);
        var member = await CreateUserAsync(roleId);
        return await ClientForAsync(member.Username);
    }

    /// <summary>A client signed in as the seeded administrator.</summary>
    protected async Task<HttpClient> AdminClientAsync()
    {
        var client = App.CreateClient(new ClientOptions { HandleCookies = false });
        await TestsHelper.SetNewAuthTokenAsync(client, TestUsers.AdminUsername, TestUsers.AdminPassword);
        return client;
    }

    /// <summary>The stored overrides of one setting, or <see langword="null"/> when it has no row.</summary>
    protected async Task<JsonObject?> StoredValuesAsync(string name)
    {
        var values = await DbContext.SettingValues
            .AsNoTracking()
            .Where(x => x.Name == name)
            .Select(x => x.Values)
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        return values is null ? null : JsonNode.Parse(values)!.AsObject();
    }

    /// <summary>Writes the stored overrides of a setting directly, standing in for an administrator's save.</summary>
    protected async Task SetStoredValuesAsync(string name, JsonObject values)
    {
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
    /// Removes every row this test may have written - directly or through an endpoint - so the
    /// next test in the collection starts from the defaults (code or configured).
    /// </summary>
    protected override async ValueTask TearDownAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var platformRows = await DbContext.SettingValues
            .ToListAsync(cancellationToken);
        if (platformRows.Count > 0)
        {
            DbContext.SettingValues.RemoveRange(platformRows);
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        await base.TearDownAsync();
    }
}