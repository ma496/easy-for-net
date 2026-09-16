namespace Backend.Tests.Features.Tenancy.Core;

using Backend.Data.Entities;
using Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests for what makes a membership active, and for what a membership is not (AC-103).
/// </summary>
/// <remarks>
/// <para>
/// "Active membership" is the only thing a tenant-scoped operation is ever allowed to rest on, so it
/// has to mean exactly one thing in exactly one place. It does: the membership row exists, is not
/// soft-deleted, and its tenant is neither suspended nor deleted - and every request recomputes that
/// from current data, which is what lets a suspension, a removal or a deletion take effect on the very
/// next request rather than when a session expires.
/// </para>
/// <para>
/// Each case below establishes the session first and changes the state afterwards, because that is the
/// only way the state being tested ever arises in practice: a session names a tenant only if the
/// account held an active membership in it at the moment the session was established.
/// </para>
/// </remarks>
public class MembershipActivationTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a membership counts as active only while the row stands, is not removed, and belongs
    /// to a tenant that is neither suspended nor deleted: the one state that lets a tenant-scoped call
    /// through, and the four that are refused with the code naming what actually happened (AC-103).
    /// </summary>
    /// <param name="state">The state the membership is left in before the tenant-scoped call.</param>
    [Theory]
    [InlineData(MembershipState.Standing)]
    [InlineData(MembershipState.Removed)]
    [InlineData(MembershipState.Erased)]
    [InlineData(MembershipState.TenantSuspended)]
    [InlineData(MembershipState.TenantDeleted)]
    public async Task Active_Membership_Definition(MembershipState state)
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);

        // Signed in while the membership is active, and acting in the tenant. The token is deliberately
        // kept: nothing below signs in again, so the refusal - when there is one - is decided by the
        // state the request found, not by the state sign-in found.
        var memberClient = await ClientForAsync(member.Username, tenant.Id);

        await ArrangeAsync(state, tenant.Id, member.Id);

        if (state == MembershipState.Standing)
        {
            var (standing, _) = await memberClient
                .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { All = true });

            standing.StatusCode.Should().Be(HttpStatusCode.OK, "a standing membership is what lets tenant-scoped work proceed");
            return;
        }

        var (refused, problem) = await memberClient
            .GETAsync<RoleListEndpoint, RoleListRequest, ProblemDetails>(new() { All = true });

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(
            ExpectedCodeFor(state),
            "the refusal names the state the membership was found in, so the client can say what happened rather than only that something did");
    }

    /// <summary>
    /// Verifies that a membership carries no per-tenant state of its own: whether a member is "active",
    /// "enabled" or "suspended" in a tenant is derived from the row and the tenant every time it is
    /// asked, so the same question cannot be answered two ways (AC-103).
    /// </summary>
    [Fact]
    public void Membership_Carries_No_Further_State()
    {
        var membershipType = DbContext.Model.FindEntityType(typeof(TenantMembership));

        membershipType.Should().NotBeNull("the tenancy schema is built from the TenantMembership entity");

        var propertyNames = membershipType!.GetProperties().Select(property => property.Name).ToList();

        // The set and not a subset: a membership is the join between an account and a tenant, the
        // soft-delete pair that ends it, and the audit and concurrency values every row carries. A
        // "Status", an "IsActive" or anything else that could disagree with the tenant it belongs to
        // would show up here as a name this list does not have.
        propertyNames.Should().BeEquivalentTo(
            ["Id", "TenantId", "UserId", "IsDeleted", "DeletedAt",
             "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "xmin"]);
    }

    /// <summary>
    /// Leaves the membership in the state a case is about. The two ways a membership can be gone are
    /// deliberately distinct: a removed membership is a soft-deleted row, while an erased one is no row
    /// at all, and a rule that only handled one of them would be a rule about rows rather than about
    /// membership.
    /// </summary>
    /// <param name="state">The state to leave the membership in.</param>
    /// <param name="tenantId">The tenant the membership belongs to.</param>
    /// <param name="userId">The account that holds it.</param>
    private async Task ArrangeAsync(MembershipState state, Guid tenantId, Guid userId)
    {
        switch (state)
        {
            case MembershipState.Standing:
                return;

            case MembershipState.Removed:
                await TenantScopedAsync(tenantId, async () =>
                {
                    var membership = await DbContext.TenantMemberships
                        .SingleAsync(candidate => candidate.UserId == userId, TestContext.Current.CancellationToken);

                    DbContext.TenantMemberships.Remove(membership);
                    await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
                });
                return;

            case MembershipState.Erased:
                // The row is deleted outright rather than soft-deleted, which is the state no code path
                // produces on its own and therefore the one that proves absence is read as absence.
                await DbContext.TenantMemberships
                    .AcrossAllTenants()
                    .Where(candidate => candidate.TenantId == tenantId && candidate.UserId == userId)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
                return;

            case MembershipState.TenantSuspended:
                await SetTenantStatusAsync(tenantId, TenantStatus.Suspended);
                return;

            case MembershipState.TenantDeleted:
                await DeleteTenantAsync(tenantId);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, "the state this case arranges has no arrangement");
        }
    }

    /// <summary>
    /// Moves a tenant into a lifecycle state, the way the suspend and reactivate surfaces do.
    /// </summary>
    /// <param name="tenantId">The tenant being moved.</param>
    /// <param name="status">The state to leave it in.</param>
    private async Task SetTenantStatusAsync(Guid tenantId, TenantStatus status)
    {
        var tenant = await DbContext.Tenants
            .SingleAsync(candidate => candidate.Id == tenantId, TestContext.Current.CancellationToken);

        tenant.Status = status;
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Retires a tenant the way the platform surface does, so the tenant reads as absent to every caller
    /// afterwards without the row having been erased.
    /// </summary>
    /// <param name="tenantId">The tenant being retired.</param>
    private async Task DeleteTenantAsync(Guid tenantId)
    {
        var tenant = await DbContext.Tenants
            .SingleAsync(candidate => candidate.Id == tenantId, TestContext.Current.CancellationToken);

        DbContext.Tenants.Remove(tenant);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The code a state's refusal is reported under.
    /// </summary>
    /// <param name="state">The state the membership was left in.</param>
    /// <returns>The error code the caller is told about.</returns>
    /// <remarks>
    /// A removed membership and an erased one are one code, not two: both are the session naming a live
    /// tenant its account no longer belongs to, and telling them apart would report how the row went
    /// missing rather than what the caller has to do about it.
    /// </remarks>
    private static string ExpectedCodeFor(MembershipState state)
        => state switch
        {
            MembershipState.Removed or MembershipState.Erased => ErrorCodes.TenantMembershipRevoked,
            MembershipState.TenantSuspended => ErrorCodes.TenantSuspended,
            MembershipState.TenantDeleted => ErrorCodes.TenantNotFound,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "the standing state is not a refusal")
        };

    /// <summary>
    /// The states a membership can be found in when a tenant-scoped request is made.
    /// </summary>
    public enum MembershipState
    {
        /// <summary>The row stands, is not removed, and its tenant is live: the only state that is active.</summary>
        Standing,

        /// <summary>The row is soft-deleted, which is how a member is removed from a tenant.</summary>
        Removed,

        /// <summary>The row is gone altogether - a state no surface produces, read the same way as a removed one.</summary>
        Erased,

        /// <summary>The row stands, but the tenant it belongs to is suspended.</summary>
        TenantSuspended,

        /// <summary>The row stands, but the tenant it belongs to is soft-deleted.</summary>
        TenantDeleted
    }
}