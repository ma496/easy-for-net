using CSharpTemplate.Common.Identity.Permissions.Provider;

namespace CSharpTemplate.App;

public class CSharpTemplatePermissionsProvider : PermissionsProvider
{
    public override void Permissions(IPermissionsContext context)
    {
        base.Permissions(context);

        var defaultGroup = context.CreateGroup("Default", "Default");

        defaultGroup.AddPermission("Permissions.ChangePassword", "ChangePassword");
        defaultGroup.AddPermission("Permissions.Developer.Tool", "Tool");
    }
}