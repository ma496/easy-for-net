namespace CSharpTemplate.Common.Identity.Permissions.Provider;

public interface IPermissionsProvider
{
    void Permissions(IPermissionsContext context);
}