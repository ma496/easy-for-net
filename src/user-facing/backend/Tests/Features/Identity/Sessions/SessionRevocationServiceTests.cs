using Backend.Features.Identity.Core.Sessions;

namespace Backend.Tests.Features.Identity.Sessions;

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
        var account = await CreateUserAsync();
        var session = await SessionForAsync(account.Username);
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            Func<Task>[] calls =
            [
                () => Revocation.RevokeUserAsync(account.Id, cancellationToken),
                () => Revocation.RevokeUsersAsync([account.Id], cancellationToken),
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
    /// Verifies revoking an account ends every session it holds, and no other account's.
    /// </summary>
    [Fact]
    public async Task RevokeUser_Ends_Every_Session_Of_The_Account()
    {
        var dual = await CreateUserAsync();
        var here = await SessionForAsync(dual.Username);
        var there = await SessionForAsync(dual.Username);
        var bystander = await SessionForAsync((await CreateUserAsync()).Username);

        await Revocation.RevokeUserAsync(dual.Id, TestContext.Current.CancellationToken);

        await AssertEndedAsync(here);
        await AssertEndedAsync(there);
        await AssertAliveAsync(bystander);
    }

    /// <summary>
    /// Verifies revoking all but one session ends the account's other sessions and keeps the named one.
    /// </summary>
    [Fact]
    public async Task RevokeUserExcept_Keeps_The_Named_Session()
    {
        var account = await CreateUserAsync();
        var kept = await SessionForAsync(account.Username);
        var other = await SessionForAsync(account.Username);

        await Revocation.RevokeUserExceptAsync(account.Id, SessionIdOf(kept), TestContext.Current.CancellationToken);

        await AssertEndedAsync(other);
        await AssertAliveAsync(kept);
    }
}
