namespace Backend.Tests.Architect;

/// <summary>
/// Architectural checks on how permissions may be gated on features. Both catch a declaration that
/// looks right and silently does nothing, or worse, hides a permission for good.
/// </summary>
public class PermissionFeatureDeclarationTests(App app) : AppTestsBase(app)
{
    /// <summary>
    /// A feature name is a string, so a typo compiles. It would then never resolve to a value, which
    /// makes the permission it gates permanently unreachable for every tenant - the kind of failure
    /// that shows up as "the permission has disappeared" long after the change that caused it.
    /// </summary>
    [Fact]
    public void Every_Feature_A_Permission_Requires_Is_Actually_Declared()
    {
        var declared = Service<IFeatureDefinitionService>().GetNames();

        var unknown = Service<IPermissionDefinitionService>()
            .GetRequiredFeaturesByPermission()
            .SelectMany(entry => entry.Value.Select(feature => (Permission: entry.Key, Feature: feature)))
            .Where(pair => !declared.Contains(pair.Feature))
            .Select(pair => $"{pair.Permission} requires '{pair.Feature}'")
            .ToList();

        unknown.Should().BeEmpty(
            "a permission gated on a feature nothing declares can never be exercised by anyone, but these are: " +
            string.Join(", ", unknown));
    }

    /// <summary>
    /// Platform scope is inside no tenant and therefore inside no plan, so nothing narrows there.
    /// Requiring a feature on a platform-scoped permission is a statement with no effect, and reads as
    /// though it had one.
    /// </summary>
    [Fact]
    public void No_Platform_Scoped_Permission_Claims_To_Require_A_Feature()
    {
        var permissionDefinitionService = Service<IPermissionDefinitionService>();
        var required = permissionDefinitionService.GetRequiredFeaturesByPermission();

        var platformScoped = permissionDefinitionService.GetFlattenedPermissions()
            .Where(permission => permission.Scope == PermissionScope.Platform
                                 && required.ContainsKey(permission.Name))
            .Select(permission => permission.Name)
            .ToList();

        platformScoped.Should().BeEmpty(
            "a platform-scoped permission is never narrowed by any tenant's plan, so requiring a feature on it is a rule that cannot apply, but these do: " +
            string.Join(", ", platformScoped));
    }
}
