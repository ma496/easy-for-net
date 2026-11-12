namespace Backend.Features.Identity.Endpoints.Account;

/// <summary>
/// This route group that prefixes all account-related endpoints with the
/// <c>account</c> URL segment (e.g. signin, signup, password management, profile).
/// </summary>
public class AccountGroup : Group
{
    public AccountGroup()
    {
        Configure("account", ep => {});
    }
}
