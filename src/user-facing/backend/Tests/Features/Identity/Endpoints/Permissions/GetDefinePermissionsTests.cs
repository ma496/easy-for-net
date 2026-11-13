using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Permissions;

namespace Backend.Tests.Features.Identity.Endpoints.Permissions;

/// <summary>
/// Tests for the <see cref="GetDefinePermissionsEndpoint"/> covering the catalogue a caller is offered:
/// the one declared in code, the same whatever the caller holds.
/// </summary>
public class GetDefinePermissionsTests(App app) : UsersTestsBase(app)
{
    /// <summary>
    /// The catalogue, read from the provider the endpoint reads it from - so what the endpoint answers is
    /// compared against the declaration rather than against another response.
    /// </summary>
    private IPermissionDefinitionService DefinitionService =>
        Service<IPermissionDefinitionService>();

    /// <summary>
    /// Verifies that the catalogue a caller is offered is the same set for two callers holding different
    /// permissions, and is the set the code declares.
    /// </summary>
    [Fact]
    public async Task Catalogue_Is_Global_And_Not_Extensible()
    {
        var administrator = await CreateUserAsync(await CreateRoleAsync(Allow.Role_View));
        var limited = await CreateUserAsync(await CreateRoleAsync(Allow.User_View));

        var forAdministrator = await CatalogueNamesAsync(await ClientForAsync(administrator.Username));
        var forLimited = await CatalogueNamesAsync(await ClientForAsync(limited.Username));

        forAdministrator.Should().BeEquivalentTo(forLimited, "the catalogue does not vary with what the caller holds");
        forAdministrator.Should().BeEquivalentTo(
            [.. DefinitionService.GetFlattenedPermissions().Select(permission => permission.Name)],
            "and it is the catalogue the code declares: the same leaves, no more and no fewer");
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
                DisplayName = definition.DisplayName
            });
            return;
        }

        foreach (var child in definition.Children)
        {
            Collect(child, leaves);
        }
    }
}
