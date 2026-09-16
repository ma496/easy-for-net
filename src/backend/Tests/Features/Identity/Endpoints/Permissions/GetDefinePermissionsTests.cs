namespace Backend.Tests.Features.Identity.Endpoints.Permissions;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Permissions;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the <see cref="GetDefinePermissionsEndpoint"/> covering the catalogue a caller is offered:
/// the one declared in code, identical in every tenant and the same on every read (AC-040), narrowed to
/// the tenant tier for a caller who could not grant a platform permission (AC-114), and returned whole
/// with the two tiers distinguishable for a platform administrator (AC-115).
/// </summary>
public class GetDefinePermissionsTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The catalogue, read from the provider the endpoint reads it from - so what the endpoint answers is
    /// compared against the declaration rather than against another response.
    /// </summary>
    private IPermissionDefinitionService DefinitionService =>
        App.Services.GetRequiredService<IPermissionDefinitionService>();

    /// <summary>
    /// Verifies that the catalogue a platform administrator is offered is the same set in each of two
    /// tenants, and is the set the code declares (AC-040).
    /// </summary>
    /// <remarks>
    /// The same caller acts in both tenants, so what is compared is one identity's view of the catalogue
    /// in two scopes rather than two identities' views of it - the difference a per-tenant catalogue
    /// would show. The set is then compared against the code-declared one in full: a response that
    /// omitted a permission, or offered one no provider declares, is as much a divergence as a
    /// difference between the two tenants.
    /// </remarks>
    [Fact]
    public async Task Catalogue_Is_Global_And_Not_Extensible()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var administrator = await CreatePlatformAdministratorAsync(first.Id, second.Id);

        var inFirst = await CatalogueNamesAsync(await ClientForAsync(administrator.Username, first.Id));
        var inSecond = await CatalogueNamesAsync(await ClientForAsync(administrator.Username, second.Id));

        inFirst.Should().BeEquivalentTo(
            inSecond,
            "the catalogue is the same for every tenant, so the tenant a caller is acting in cannot be read off it");

        inFirst.Should().BeEquivalentTo(
            [.. DefinitionService.GetFlattenedPermissions().Select(permission => permission.Name)],
            "and it is the catalogue the code declares: the same leaves, no more and no fewer");
    }

    /// <summary>
    /// Verifies that a caller without platform administration is offered tenant-tier permissions alone,
    /// so no permission it could never grant through a tenant role is put in front of it (AC-114).
    /// </summary>
    /// <remarks>
    /// The caller administers a tenant of its own, which is what makes the filter meaningful: it is
    /// exactly the caller the role-permission surface is meant for. The set is compared against the
    /// tenant tier of the declaration rather than merely examined for the platform one, because both
    /// halves matter - a platform permission leaking in would offer the caller something it cannot grant,
    /// and a tenant permission dropped would hide one it can.
    /// </remarks>
    [Fact]
    public async Task Tenant_Caller_Receives_Only_Tenant_Permissions()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_View));

        var names = await CatalogueNamesAsync(await ClientForAsync(administrator.Username));

        var platformNames = DefinitionService.GetPlatformPermissionNames();
        platformNames.Should().NotBeEmpty("the catalogue declares a platform tier, which is what this test is about");

        names.Should().NotIntersectWith(
            platformNames,
            "a permission that governs the installation cannot be granted through a tenant role, so offering it would be offering what the caller cannot do");

        names.Should().BeEquivalentTo(
            [.. Flatten(DefinitionService.GetPermissionGroups(includePlatformPermissions: false)).Select(permission => permission.Name)],
            "the caller is offered the tenant tier of the catalogue, whole");
    }

    /// <summary>
    /// Verifies that a platform administrator is offered the whole catalogue, with the platform tier
    /// flagged so the two tiers stay apart (AC-115).
    /// </summary>
    /// <remarks>
    /// The flag is asserted leaf by leaf rather than only for the platform ones: a catalogue that flagged
    /// everything, or nothing, would leave the tiers indistinguishable just as surely as one that left the
    /// platform permissions out. The set is compared against the declaration on top of that, so the
    /// widening is the same catalogue rather than a differently shaped one.
    /// </remarks>
    [Fact]
    public async Task Platform_Caller_Receives_The_Whole_Catalogue()
    {
        await SetAuthTokenAsync();

        var (response, catalogue) = await App.Client
            .GETAsync<GetDefinePermissionsEndpoint, GetDefinePermissionsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var leaves = Flatten(catalogue.Groups);
        var platformNames = DefinitionService.GetPlatformPermissionNames();

        platformNames.Should().NotBeEmpty("the catalogue declares a platform tier, which is what the caller is being widened to");

        leaves.Select(permission => permission.Name).Should().BeEquivalentTo(
            [.. DefinitionService.GetFlattenedPermissions().Select(permission => permission.Name)],
            "a platform administrator is offered the catalogue whole");

        leaves.Where(permission => platformNames.Contains(permission.Name))
            .Should().OnlyContain(permission => permission.IsPlatform,
                "every permission of the platform tier is flagged as such, so a caller reading the answer can tell what a tenant role can be given");

        leaves.Where(permission => !platformNames.Contains(permission.Name))
            .Should().OnlyContain(permission => !permission.IsPlatform,
                "and no tenant permission is flagged as platform-tier, which would keep it off every tenant role in the editor");
    }

    /// <summary>
    /// An account holding platform administration and a membership of each tenant named, so that one
    /// identity can be seen acting in either - which is what lets the catalogue be compared between two
    /// tenants as one caller's view of it. The account is made for this test: no seeded account belongs to
    /// two tenants except the one the suite reserves for the chooser, and adding a membership to a seeded
    /// account would change the tenants every other test signs in to.
    /// </summary>
    /// <param name="firstTenantId">The first tenant the account is to be a member of.</param>
    /// <param name="secondTenantId">The second tenant the account is to be a member of.</param>
    /// <returns>The created account.</returns>
    private async Task<User> CreatePlatformAdministratorAsync(Guid firstTenantId, Guid secondTenantId)
    {
        var account = await CreateAccountWithoutMembershipAsync();

        // The platform role is the one that belongs to no tenant, so it is assigned to the account
        // rather than granted through a membership: a tenant's role cannot carry it, and a tenant's
        // membership is not where platform authority lives.
        await UserService.AssignRoleAsync(account.Id, TestRoles.PlatformAdminRoleId);

        // The memberships are what the account switches by, and they are held through a role of the
        // tenant it is joining - platform authority is not what admits it to either.
        await MembershipService.AddAsync(
            firstTenantId,
            account.Id,
            [await CreateTenantRoleAsync(firstTenantId, Allow.Tenant_View)],
            TestContext.Current.CancellationToken);
        await MembershipService.AddAsync(
            secondTenantId,
            account.Id,
            [await CreateTenantRoleAsync(secondTenantId, Allow.Tenant_View)],
            TestContext.Current.CancellationToken);

        return account;
    }

    /// <summary>
    /// The permission names the endpoint offers a caller, flattened over the groups and the nested
    /// definitions it returns.
    /// </summary>
    /// <param name="client">The client presenting the caller's token.</param>
    /// <returns>The permission names in the answer.</returns>
    private static async Task<List<string>> CatalogueNamesAsync(HttpClient client)
    {
        var (response, catalogue) = await client
            .GETAsync<GetDefinePermissionsEndpoint, GetDefinePermissionsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return [.. Flatten(catalogue.Groups).Select(permission => permission.Name)];
    }

    /// <summary>
    /// The leaves of a returned catalogue, read the way the role-permission surface reads it: a
    /// definition with children names a branch, and only the definitions without children name
    /// permissions.
    /// </summary>
    /// <param name="groups">The groups the endpoint returned.</param>
    /// <returns>Every leaf permission in them.</returns>
    private static List<FlattenedPermission> Flatten(IEnumerable<PermissionGroupDefinition> groups)
    {
        var leaves = new List<FlattenedPermission>();
        foreach (var group in groups)
        {
            foreach (var permission in group.Permissions)
            {
                Collect(permission, leaves);
            }
        }

        return leaves;
    }

    /// <summary>
    /// Adds a definition's leaves to <paramref name="leaves"/>, descending into its children.
    /// </summary>
    /// <param name="definition">The definition to read.</param>
    /// <param name="leaves">The list the leaves are collected into.</param>
    private static void Collect(PermissionDefinition definition, List<FlattenedPermission> leaves)
    {
        if (definition.Children.Count == 0)
        {
            leaves.Add(new FlattenedPermission
            {
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                IsPlatform = definition.IsPlatform
            });
            return;
        }

        foreach (var child in definition.Children)
        {
            Collect(child, leaves);
        }
    }
}
