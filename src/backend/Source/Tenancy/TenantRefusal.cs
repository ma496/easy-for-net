namespace Backend.Tenancy;

using Backend.Features.Identity.Core;

/// <summary>
/// The one description of why a tenant-scoped request was refused for want of a usable tenant: the
/// message and the error code the caller is answered with.
/// </summary>
/// <remarks>
/// <para>
/// It is stated once and read by both points that can refuse such a request, because there are two and
/// there has to be. Whether a request reaches the tenant enforcement point at all depends on whether
/// endpoint authorization let it through, and authorization evaluates the permissions the request
/// currently holds - which for a caller whose tenant selection has gone stale are none, since the
/// session check recomputes them against no tenant. So the refusal is decided by authorization before
/// the enforcement point is ever reached, and both have to report the same thing.
/// </para>
/// <para>
/// A caller that does keep a grant through a stale selection - a platform administrator, whose
/// platform-scoped roles are conferred by no tenant and therefore withdrawn by none - passes
/// authorization and is refused by the enforcement point instead. The two paths differ in which code
/// answers a request; they do not differ in what the caller is told, which is what this states once.
/// </para>
/// </remarks>
public static class TenantRefusal
{
    /// <summary>
    /// Maps the state of a request's tenant selection to the message and the error code the caller is
    /// refused with, so a refusal says which of the four things went wrong rather than only that
    /// something did, and the web app can offer the caller another tenant to work in.
    /// </summary>
    /// <param name="status">The state of the request's tenant selection.</param>
    /// <returns>The message and the error code to report.</returns>
    /// <remarks>
    /// <see cref="ErrorCodes.NotTenantMember"/> is not among them, and that is not an omission: a
    /// session names a tenant only if the account held a membership in it when the session was
    /// established, so a membership missing here is always one that was revoked since. The endpoints
    /// that address a tenant by route id, where the caller may never have been a member of it, report
    /// that code themselves.
    /// </remarks>
    public static (string Message, string ErrorCode) Describe(TenantSessionStatus status)
        => status switch
        {
            TenantSessionStatus.TenantNotFound => (
                "The tenant this session was working in no longer exists. Choose another tenant to continue.",
                ErrorCodes.TenantNotFound),
            TenantSessionStatus.TenantSuspended => (
                "The tenant this session is working in is suspended. Choose another tenant to continue.",
                ErrorCodes.TenantSuspended),
            TenantSessionStatus.MembershipRevoked => (
                "Your membership of the tenant this session is working in has been removed. Choose another tenant to continue.",
                ErrorCodes.TenantMembershipRevoked),
            _ => (
                "No active tenant is selected for this session. Choose a tenant you belong to before continuing.",
                ErrorCodes.NoActiveTenant)
        };
}