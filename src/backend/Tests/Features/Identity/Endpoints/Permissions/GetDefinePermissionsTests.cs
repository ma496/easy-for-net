namespace Backend.Tests.Features.Identity.Endpoints.Permissions;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Permissions;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the <see cref="GetDefinePermissionsEndpoint"/> covering the catalogue a caller is offered:
/// the one declared in code, identical in every tenant and the same on every read (AC-040), narrowed to
/// what a tenant role can hold for a caller acting inside a tenant (AC-114), and narrowed to the
/// platform scope, with each leaf carrying the scope it was declared with, for a platform account
/// acting in none (AC-115).
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
    /// Verifies that the catalogue a caller is offered is the same set in each of two tenants, and is
    /// the set the code declares for a caller acting inside one (AC-040).
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
            [.. DefinitionService.GetPermissionNamesInScope(PermissionScope.Tenant)],
            "and it is the catalogue the code declares, narrowed to what can be exercised inside a tenant: the same leaves, no more and no fewer");
    }

    /// <summary>
    /// Verifies that a caller acting inside a tenant is offered the permissions exercisable there alone,
    /// so no permission it could never grant through a tenant role is put in front of it (AC-114).
    /// </summary>
    /// <remarks>
    /// The caller administers a tenant of its own, which is what makes the filter meaningful: it is
    /// exactly the caller the role-permission surface is meant for. The set is compared against the
    /// tenant scope of the declaration rather than merely examined for the platform one, because both
    /// halves matter - a platform permission leaking in would offer the caller something it cannot grant,
    /// and a tenant permission dropped would hide one it can.
    /// </remarks>
    [Fact]
    public async Task Tenant_Caller_Receives_Only_Tenant_Scoped_Permissions()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_View));

        var names = await CatalogueNamesAsync(await ClientForAsync(administrator.Username));

        var platformNames = PlatformOnlyPermissionNames();
        platformNames.Should().NotBeEmpty("the catalogue declares a platform scope, which is what this test is about");

        names.Should().NotIntersectWith(
            platformNames,
            "a permission that governs the installation cannot be granted through a tenant role, so offering it would be offering what the caller cannot do");

        names.Should().BeEquivalentTo(
            [.. Flatten(DefinitionService.GetPermissionGroups(PermissionScope.Tenant)).Select(permission => permission.Name)],
            "the caller is offered what a tenant role can exercise - the tenant scope and the permissions declared for both - whole");
    }

    /// <summary>
    /// Verifies that a platform account acting in no tenant is offered the platform scope of the
    /// catalogue, each leaf carrying the scope it was declared with so the tiers stay apart (AC-115).
    /// </summary>
    /// <remarks>
    /// The scope is asserted leaf by leaf rather than only for the platform ones: a catalogue that
    /// reported one scope for everything would leave the tiers indistinguishable just as surely as one
    /// that left the platform permissions out. The set is compared against the declaration on top of
    /// that, so what the caller sees is the same catalogue narrowed rather than a differently shaped one.
    /// The tenant-only scope is what such a caller does not see: it cannot be exercised where they are.
    /// </remarks>
    [Fact]
    public async Task Platform_Caller_Receives_The_Platform_Scope_Of_The_Catalogue()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, catalogue) = await App.Client
            .GETAsync<GetDefinePermissionsEndpoint, GetDefinePermissionsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var leaves = Flatten(catalogue.Groups);
        var platformNames = PlatformOnlyPermissionNames();

        platformNames.Should().NotBeEmpty("the catalogue declares a platform scope, which is what this caller is offered");

        leaves.Select(permission => permission.Name).Should().BeEquivalentTo(
            [.. DefinitionService.GetPermissionNamesInScope(PermissionScope.Platform)],
            "a platform account acting in no tenant is offered what it can exercise there - the platform scope and the permissions declared for both");

        leaves.Select(permission => permission.Name).Should().Contain(platformNames,
            "including every permission only a platform role can hold, which is the half a tenant caller never sees");

        leaves.Where(permission => platformNames.Contains(permission.Name))
            .Should().OnlyContain(permission => permission.Scope == PermissionScope.Platform,
                "every platform-scoped permission reports that scope, so a caller reading the answer can tell what a tenant role can be given");

        leaves.Where(permission => !platformNames.Contains(permission.Name))
            .Should().OnlyContain(permission => permission.Scope == PermissionScope.Both,
                "and everything else it is offered is exercisable in either scope, which is why it appears here at all");
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
        await MarkAsPlatformAccountAsync(account.Id);

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
                Scope = definition.Scope
            });
            return;
        }

        foreach (var child in definition.Children)
        {
            Collect(child, leaves);
        }
    }
}
