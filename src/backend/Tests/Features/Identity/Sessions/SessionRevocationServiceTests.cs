namespace Backend.Tests.Features.Identity.Sessions;

using Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Tests for <see cref="ISessionRevocationService"/> itself: what each member ends, what it leaves, and that
/// it refuses to run inside an open transaction.
/// </summary>
public class SessionRevocationServiceTests(App app) : SessionRevocationTestsBase(app)
{
    private ISessionRevocationService Revocation => Service<ISessionRevocationService>();

    /// <summary>
    /// Verifies every member refuses to run while a transaction is open on the scope's context, and that after
    /// the rollback the sessions it would have ended still work - a revocation cannot be undone, a change can.
    /// </summary>
    [Fact]
    public async Task An_Open_Transaction_Is_Refused_And_Nothing_Is_Revoked()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        var session = await SessionForAsync(account.Username, tenant.Id);
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            Func<Task>[] calls =
            [
                () => Revocation.RevokeUserAsync(account.Id, cancellationToken),
                () => Revocation.RevokeUserInScopeAsync(account.Id, tenant.Id, cancellationToken),
                () => Revocation.RevokeTenantAsync(tenant.Id, cancellationToken),
                () => Revocation.RevokeUsersInScopeAsync([account.Id], tenant.Id, cancellationToken),
                () => Revocation.RevokeUserExceptAsync(account.Id, "another-session", cancellationToken),
            ];

            foreach (var call in calls)
            {
                await call.Should().ThrowAsync<InvalidOperationException>();
            }

            await transaction.RollbackAsync(cancellationToken);
        }

        await AssertAliveAsync(session);
    }

    /// <summary>
    /// Verifies revoking an account ends every session it holds, in every tenant, and no other account's.
    /// </summary>
    [Fact]
    public async Task RevokeUser_Ends_Every_Session_Of_The_Account()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var dual = await CreateDualTenantMemberAsync(tenant.Id, otherTenant.Id);
        var here = await SessionForAsync(dual.Username, tenant.Id);
        var there = await SessionForAsync(dual.Username, otherTenant.Id);
        var bystander = await SessionForAsync((await CreateTenantUserAsync(tenant.Id)).Username, tenant.Id);

        await Revocation.RevokeUserAsync(dual.Id, TestContext.Current.CancellationToken);

        await AssertEndedAsync(here);
        await AssertEndedAsync(there);
        await AssertAliveAsync(bystander);
    }

    /// <summary>
    /// Verifies revoking an account in one tenant ends its sessions there and leaves its session in another.
    /// </summary>
    [Fact]
    public async Task RevokeUserInScope_Leaves_The_Accounts_Sessions_In_Other_Tenants()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var dual = await CreateDualTenantMemberAsync(tenant.Id, otherTenant.Id);
        var here = await SessionForAsync(dual.Username, tenant.Id);
        var there = await SessionForAsync(dual.Username, otherTenant.Id);

        await Revocation.RevokeUserInScopeAsync(dual.Id, tenant.Id, TestContext.Current.CancellationToken);

        await AssertEndedAsync(here);
        await AssertAliveAsync(there);
    }

    /// <summary>
    /// Verifies platform scope (a null tenant) ends the account's session acting in no tenant and leaves the
    /// one it holds inside a tenant it is a member of.
    /// </summary>
    [Fact]
    public async Task RevokeUserInScope_With_No_Tenant_Ends_Only_The_Platform_Scope_Sessions()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        await MarkAsPlatformAccountAsync(account.Id);
        var platformSession = await SessionForAsync(account.Username);
        var tenantSession = await SessionForAsync(account.Username, tenant.Id);

        await Revocation.RevokeUserInScopeAsync(account.Id, null, TestContext.Current.CancellationToken);

        await AssertEndedAsync(platformSession);
        await AssertAliveAsync(tenantSession);
    }

    /// <summary>
    /// Verifies revoking a tenant ends every session acting in it, whoever holds it, and none in another tenant.
    /// </summary>
    [Fact]
    public async Task RevokeTenant_Ends_Every_Session_Acting_In_The_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(tenant.Id);
        var dual = await CreateDualTenantMemberAsync(tenant.Id, otherTenant.Id);
        var memberSession = await SessionForAsync(member.Username, tenant.Id);
        var dualHere = await SessionForAsync(dual.Username, tenant.Id);
        var dualThere = await SessionForAsync(dual.Username, otherTenant.Id);
        var otherMember = await SessionForAsync((await CreateTenantUserAsync(otherTenant.Id)).Username, otherTenant.Id);

        await Revocation.RevokeTenantAsync(tenant.Id, TestContext.Current.CancellationToken);

        await AssertEndedAsync(memberSession);
        await AssertEndedAsync(dualHere);
        await AssertAliveAsync(dualThere);
        await AssertAliveAsync(otherMember);
    }

    /// <summary>
    /// Verifies revoking several accounts in a scope ends theirs and leaves an account not named.
    /// </summary>
    [Fact]
    public async Task RevokeUsersInScope_Ends_The_Named_Accounts_Sessions()
    {
        var tenant = await CreateTenantAsync();
        var first = await CreateTenantUserAsync(tenant.Id);
        var second = await CreateTenantUserAsync(tenant.Id);
        var firstSession = await SessionForAsync(first.Username, tenant.Id);
        var secondSession = await SessionForAsync(second.Username, tenant.Id);
        var bystander = await SessionForAsync((await CreateTenantUserAsync(tenant.Id)).Username, tenant.Id);

        await Revocation.RevokeUsersInScopeAsync([first.Id, second.Id], tenant.Id, TestContext.Current.CancellationToken);

        await AssertEndedAsync(firstSession);
        await AssertEndedAsync(secondSession);
        await AssertAliveAsync(bystander);
    }

    /// <summary>
    /// Verifies revoking all but one session ends the account's other sessions and keeps the named one.
    /// </summary>
    [Fact]
    public async Task RevokeUserExcept_Keeps_The_Named_Session()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        var kept = await SessionForAsync(account.Username, tenant.Id);
        var other = await SessionForAsync(account.Username, tenant.Id);

        await Revocation.RevokeUserExceptAsync(account.Id, SessionIdOf(kept), TestContext.Current.CancellationToken);

        await AssertEndedAsync(other);
        await AssertAliveAsync(kept);
    }
}
