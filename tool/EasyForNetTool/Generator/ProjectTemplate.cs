namespace EasyForNetTool.Generator;

/// <summary>
/// One of the application templates the repository carries under <c>src/&lt;Name&gt;</c>, and what is
/// different about a project generated from it. Everything not named here — the root files, the task
/// loop, <c>.claude</c> — is shared, and a project's layout is <c>src/backend</c> + <c>src/frontend/web</c>
/// whichever template it came from.
/// </summary>
/// <param name="Name">The directory under <c>src/</c>, and the value of <c>--template</c>.</param>
/// <param name="ClaudeResource">The embedded file written as the project's <c>CLAUDE.md</c>.</param>
/// <param name="ReadmeResource">The embedded file written as the project's <c>README.md</c>.</param>
/// <param name="ExcludedSkills">Skills under <c>.claude/skills</c> that describe something the template does not have.</param>
/// <param name="DevHosts">The hosts the development proxy is opened on, written to <c>agentic.config.json</c>.</param>
/// <param name="SeededAccounts">The accounts the data seeder creates, as printed after generation.</param>
/// <param name="ExamplePagesPath">The component example pages, relative to the web app, deleted unless <c>-e true</c>.</param>
public sealed record ProjectTemplate(
    string Name,
    string ClaudeResource,
    string ReadmeResource,
    IReadOnlyList<string> ExcludedSkills,
    IReadOnlyList<string> DevHosts,
    string SeededAccounts,
    IReadOnlyList<string> ExamplePagesPath)
{
    /// <summary>The multi-tenant SaaS: tenants on their own hosts, editions and features. The default.</summary>
    public static readonly ProjectTemplate MultiTenant = new(
        "multi-tenant",
        "new-project-claude.md",
        "new-project-readme.md",
        [],
        ["app.localhost", "default.localhost"],
        "admin and tenantadmin",
        ["app", "[lang]", "(app)", "ui"]);

    /// <summary>The single-tenant, user-facing web app: public SEO pages at <c>/</c>, administration at <c>/admin</c>.</summary>
    public static readonly ProjectTemplate UserFacing = new(
        "user-facing",
        "user-facing-claude.md",
        "user-facing-readme.md",
        ["multi-tenancy", "feature-management"],
        ["localhost"],
        "admin",
        ["app", "[lang]", "admin", "ui"]);

    /// <summary>Every template, the default first.</summary>
    public static readonly IReadOnlyList<ProjectTemplate> All = [MultiTenant, UserFacing];

    /// <summary>The directory, under the template repository's <c>src/</c>, whose files replace shared root files in a generated project.</summary>
    public const string OverlayDirectory = "overlay";

    /// <summary>
    /// The template named <paramref name="name"/>, compared case-insensitively; <see cref="MultiTenant"/> when it is empty.
    /// </summary>
    /// <exception cref="UserFriendlyException">No template has that name.</exception>
    public static ProjectTemplate Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return MultiTenant;
        return All.FirstOrDefault(t => string.Equals(t.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new UserFriendlyException($"Unknown template '{name}'. Use one of: {string.Join(", ", All.Select(t => t.Name))}.");
    }
}
