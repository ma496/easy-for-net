namespace Backend.Features.Seo.Core;

/// <summary>
/// Declares the permission hierarchy for the Seo feature: viewing and editing the SEO pages.
/// </summary>
/// <remarks>
/// The public endpoints the web app renders metadata, the sitemap and robots.txt from are anonymous;
/// these guard the admin screen alone. The site-wide defaults are the <c>Seo</c> setting, edited under
/// <see cref="Allow.Settings_Update"/>.
/// </remarks>
public class SeoPermissionsProvider : IPermissionDefinitionProvider
{
    public string GroupName => "Seo";

    public void Define(PermissionDefinitionContext context)
    {
        var seoPermissions = context.AddPermission("Seo", "Seo");
        seoPermissions.AddChild(Allow.Seo_View, "View");
        seoPermissions.AddChild(Allow.Seo_Update, "Update");
    }
}
