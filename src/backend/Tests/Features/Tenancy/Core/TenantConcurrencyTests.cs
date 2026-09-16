namespace Backend.Tests.Features.Tenancy.Core;

using Backend.Data.Entities;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Backend.Tenancy;

/// <summary>
/// Tests for two requests changing the same member's role assignments at once - a whole set survives,
/// never a mixture of the two (AC-081).
/// </summary>
/// <remarks>
/// <para>
/// The replacement is the one write in the tenancy surfaces that reads a set, decides a set, and writes
/// it back, so it is the one that can interleave: two administrators re-roling the same member could
/// otherwise each revoke what the other granted and between them leave a set neither asked for. What
/// prevents it is the concurrency token on the membership row, which every replacement writes, so a
/// caller who read the row before another caller changed it fails visibly rather than overwriting a set
/// it never saw.
/// </para>
/// <para>
/// The first test drives that through two HTTP clients at once and asserts the outcome that matters -
/// the persisted set is one of the two complete sets - while allowing for the fact that two requests
/// started together may still be served one after the other, in which case both legitimately succeed
/// and the later set is the one in force. The second test proves the mechanism itself deterministically:
/// a replacement made with a token read before another change is refused rather than applied.
/// </para>
/// </remarks>
public class TenantConcurrencyTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that two concurrent replacements of one member's roles persist one of the two complete
    /// sets rather than a mixture of them, and that the loser - when there is one - is told the member's
    /// roles were changed by another request rather than being handed a set nobody asked for (AC-081).
    /// </summary>
    [Fact]
    public async Task Concurrent_Assignment_Updates_Keep_One_Complete_Set()
    {
        var arrangement = await ArrangeMemberWithFourRolesAsync();

        // Two clients, because the fixture's own client carries one bearer token for the whole test and
        // a request cannot be in flight twice on one token's behalf. The identity behind them is the
        // same administrator, so what differs between the two calls is only the set asked for.
        var callerA = await ClientForAsync(arrangement.Administrator.Username, arrangement.TenantId);
        var callerB = await ClientForAsync(arrangement.Administrator.Username, arrangement.TenantId);

        var firstSet = new[] { arrangement.Roles[0].Id, arrangement.Roles[1].Id };
        var secondSet = new[] { arrangement.Roles[2].Id, arrangement.Roles[3].Id };

        var answers = await Task.WhenAll(
            ReplaceRolesAsync(callerA, arrangement.TenantId, arrangement.Member.Id, firstSet),
            ReplaceRolesAsync(callerB, arrangement.TenantId, arrangement.Member.Id, secondSet));

        // A request is either applied or refused as a concurrent modification - never refused for some
        // other reason, which would mean this test had stopped exercising the path it is about.
        answers.Should().OnlyContain(answer =>
            answer.Status == HttpStatusCode.OK
            || (answer.Status == HttpStatusCode.BadRequest && answer.Code == ErrorCodes.ConcurrentModification));

        answers.Should().Contain(
            answer => answer.Status == HttpStatusCode.OK,
            "at least one replacement has to have been applied for the set below to be worth asserting");

        var persisted = await ReadAssignmentsAsync(arrangement.Member.Id, arrangement.Roles);

        new[] { firstSet, secondSet }
            .Should()
            .Contain(set => set.Order().SequenceEqual(persisted.Order()),
                     "the member holds one of the two complete sets, and no mixture of them");

        persisted.Should().HaveCount(2, "neither request asked for a set of any other size");
    }

    /// <summary>
    /// Verifies that a replacement made from a membership row read before another change to it is
    /// refused rather than applied, which is what makes the set above whole: the later writer fails on
    /// the row's concurrency token instead of overwriting a set it never saw.
    /// </summary>
    [Fact]
    public async Task Concurrent_Replacement_Is_Detected_By_The_Membership_Token()
    {
        var arrangement = await ArrangeMemberWithFourRolesAsync();

        // Two independent units of work, each with the context and the tenant scope a request of its
        // own would have - which is what lets two reads of one row really straddle a change to it.
        using var staleScope = App.Services.CreateScope();
        using var currentScope = App.Services.CreateScope();

        var staleDbContext = staleScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var staleTenantContext = staleScope.ServiceProvider.GetRequiredService<ITenantContext>();
        var currentDbContext = currentScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var currentTenantContext = currentScope.ServiceProvider.GetRequiredService<ITenantContext>();

        TenantMembership stale;
        using (staleTenantContext.BeginTenant(arrangement.TenantId))
        {
            stale = await LoadMembershipAsync(staleDbContext, arrangement.TenantId, arrangement.Member.Id);
        }

        using (currentTenantContext.BeginTenant(arrangement.TenantId))
        {
            var current = await LoadMembershipAsync(currentDbContext, arrangement.TenantId, arrangement.Member.Id);

            // Any write to the row moves the token: the values written are the ones already there, and
            // the row's system column changes all the same, which is exactly what a real replacement
            // relies on.
            currentDbContext.TenantMemberships.Update(current);
            await currentDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (staleTenantContext.BeginTenant(arrangement.TenantId))
        {
            staleDbContext.TenantMemberships.Update(stale);

            var saved = async () => await staleDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await saved.Should().ThrowAsync<DbUpdateConcurrencyException>(
                "the row was changed after it was read, so this write must lose rather than overwrite it");
        }
    }

    /// <summary>
    /// Replaces a member's roles through the endpoint, reporting the status and - when it was refused -
    /// the code the refusal carried.
    /// </summary>
    /// <param name="client">The administrator making the request.</param>
    /// <param name="tenantId">The tenant the member is being re-roled in.</param>
    /// <param name="userId">The member whose roles are being replaced.</param>
    /// <param name="roleIds">The complete set of roles the member is to hold.</param>
    private static async Task<Replacement> ReplaceRolesAsync(HttpClient client, Guid tenantId, Guid userId, Guid[] roleIds)
    {
        var request = new TenantMemberUpdateRolesRequest
        {
            TenantId = tenantId,
            UserId = userId,
            Roles = [.. roleIds]
        };

        var (response, problem) = await client
            .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, ProblemDetails>(request);

        return new Replacement(response.StatusCode, problem?.Errors?.FirstOrDefault()?.Code);
    }

    /// <summary>
    /// Reads a membership through a context of the caller's own, inside the tenant's scope.
    /// </summary>
    /// <param name="dbContext">The unit of work the row is read in.</param>
    /// <param name="tenantId">The tenant the membership belongs to.</param>
    /// <param name="userId">The account whose membership is read.</param>
    private static async Task<TenantMembership> LoadMembershipAsync(AppDbContext dbContext, Guid tenantId, Guid userId)
        => await dbContext.TenantMemberships
            .AcrossAllTenants()
            .SingleAsync(membership => membership.TenantId == tenantId && membership.UserId == userId,
                         TestContext.Current.CancellationToken);

    /// <summary>
    /// The roles one member holds out of the set a test arranged, read back from the database rather
    /// than from any context that made the change.
    /// </summary>
    /// <param name="userId">The member whose assignments are read.</param>
    /// <param name="roles">The roles the test arranged, which are the only ones being asked about.</param>
    private async Task<List<Guid>> ReadAssignmentsAsync(Guid userId, IReadOnlyCollection<Role> roles)
    {
        var arranged = roles.Select(role => role.Id).ToList();

        return await DbContext.UserRoles
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId && arranged.Contains(assignment.RoleId))
            .Select(assignment => assignment.RoleId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Arranges a tenant, an administrator of it holding the permission that lets them re-role a member,
    /// a member holding nothing, and four roles of the tenant for the two requests to choose between.
    /// The administrator is created first, so the tenant has somebody able to administer it before the
    /// member is added and the last-administrator guard has nothing to refuse.
    /// </summary>
    private async Task<ConcurrencyArrangement> ArrangeMemberWithFourRolesAsync()
    {
        var tenant = await CreateTenantAsync();

        var administratorRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.TenantMember_UpdateRoles);
        var administrator = await CreateTenantUserAsync(tenant.Id, administratorRoleId);

        var member = await CreateTenantUserAsync(tenant.Id);

        var roles = new List<Role>();
        for (var index = 0; index < 4; index++)
        {
            roles.Add(await CreateRoleAsync(tenant.Id));
        }

        return new ConcurrencyArrangement(tenant.Id, administrator, member, roles);
    }

    /// <summary>
    /// Creates one role inside a tenant holding no permission, since the requests below only ever choose
    /// between roles and never act through them.
    /// </summary>
    /// <param name="tenantId">The tenant the role belongs to.</param>
    private async Task<Role> CreateRoleAsync(Guid tenantId)
    {
        using var tenantScope = TenantContext.BeginTenant(tenantId);

        var role = new Role
        {
            SystemCreated = false,
            Name = $"concurrent-{Guid.NewGuid():N}",
            Description = "Role arranged by a concurrency test"
        };
        DbContext.Roles.Add(role);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return role;
    }

    /// <summary>
    /// What a replacement answered: the status, and the code the refusal carried when it was refused.
    /// </summary>
    /// <param name="Status">The status the endpoint answered with.</param>
    /// <param name="Code">The error code of the refusal, or <see langword="null"/> when it succeeded.</param>
    private sealed record Replacement(HttpStatusCode Status, string? Code);

    /// <summary>
    /// The tenant, administrator, member and roles one concurrency test arranged.
    /// </summary>
    /// <param name="TenantId">The tenant the member is being re-roled in.</param>
    /// <param name="Administrator">The account able to re-role members of the tenant.</param>
    /// <param name="Member">The member whose roles the two requests replace.</param>
    /// <param name="Roles">The four roles each request chooses its set from.</param>
    private sealed record ConcurrencyArrangement(Guid TenantId, User Administrator, User Member, List<Role> Roles);
}