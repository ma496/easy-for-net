namespace Backend.Tests.Features.Tenancy.Core;

using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests for which tenant a request acts in - exactly one, and never a guess (AC-022, AC-023, AC-050).
/// </summary>
/// <remarks>
/// <para>
/// The question "which tenant is this request for?" is settled before a session exists at all. A
/// session that names a tenant acts in it and in no other; an ordinary account that cannot be placed
/// in exactly one - because it belongs to several, or to none - is refused at sign-in and told to name
/// the tenant it means, rather than being signed in to a session in which nothing it tries can work.
/// </para>
/// <para>
/// The seeded accounts are named rather than built, because these are the states only seeding can
/// produce: <c>dual</c> holds an active membership in two tenants - the bootstrap tenant and the second
/// one - and <c>nomember</c> holds none at all. A test-built account can reach neither state without
/// first breaking the one-membership invariant the rest of the suite signs in on.
/// </para>
/// </remarks>
public class TenantContextResolutionTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The seeded account holding an active membership in two tenants, and therefore no selected tenant
    /// until it chooses one.
    /// </summary>
    private const string DualMembershipUsername = "dual";

    /// <summary>
    /// The seeded account holding no membership at all.
    /// </summary>
    private const string NoMembershipUsername = "nomember";

    /// <summary>
    /// Verifies that a request made by an account that belongs to two tenants and has selected one acts
    /// in that one alone: the row it creates is attributed to the selected tenant, and no row is written
    /// into the tenant it also belongs to (AC-022).
    /// </summary>
    [Fact]
    public async Task Request_Acts_In_Exactly_One_Tenant()
    {
        // Nothing here arranges a tenant: both are the ones `dual` was seeded into, so what is proved is
        // the selection between them rather than a tenant this test made up.
        var selectedTenantId = TestTenants.SecondTenantId;
        var otherTenantId = TestTenants.BootstrapTenantId;

        var dual = await ClientForAsync(DualMembershipUsername, selectedTenantId);
        var name = $"one-tenancy-{Guid.NewGuid():N}";

        var (response, _) = await dual
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(new()
            {
                Name = name,
                Description = "Role created to prove which tenant the request acted in"
            });

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the account administers the tenant it selected, so the request is one it may make");

        var stored = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.NameNormalized == name.ToLowerInvariant())
            .Select(role => new { role.Id, role.TenantId })
            .ToListAsync(TestContext.Current.CancellationToken);

        stored.Should().ContainSingle("the request wrote into one tenant, not into both of them");
        stored[0].TenantId.Should().Be(selectedTenantId, "and it is the tenant the session selected");
        stored[0].TenantId.Should().NotBe(otherTenantId, "the other tenant the account belongs to was left untouched");
    }

    /// <summary>
    /// Verifies that an account belonging to two tenants and naming neither is refused at sign-in and
    /// asked which tenant it means, rather than signed in to a session that has selected none (AC-023).
    /// </summary>
    [Fact]
    public async Task Several_Memberships_And_No_Tenant_Named_Is_Refused_At_Sign_In()
    {
        var visitor = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (response, problem) = await visitor
            .POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(new()
            {
                Username = DualMembershipUsername,
                Password = TestUsers.DefaultPassword
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the account belongs to two tenants, so which one it came to work in is a question only it can answer");
        problem.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ErrorCodes.TenantRequired,
                "the caller is told what is missing so the sign-in screen can ask for the tenant rather than show a bare failure");

        // Naming one settles it, which is the point of the refusal: it is a question, not a lock-out.
        var identifier = await TenantIdentifierOfAsync(TestTenants.SecondTenantId);

        var (named, session) = await visitor
            .POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(new()
            {
                Username = DualMembershipUsername,
                Password = TestUsers.DefaultPassword,
                TenantIdentifier = identifier
            });

        named.StatusCode.Should().Be(HttpStatusCode.OK);
        session.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Verifies that an account holding no membership in any active tenant cannot sign in at all, and is
    /// told that a tenant is what it is missing (AC-050).
    /// </summary>
    /// <remarks>
    /// An ordinary account with no tenant could exercise no permission whatever, so a session for it
    /// would authenticate somebody and then refuse everything they went on to do. The refusal is moved
    /// to the one place that can say something useful about it.
    /// </remarks>
    [Fact]
    public async Task Account_With_No_Membership_Cannot_Sign_In()
    {
        var visitor = App.CreateClient(new ClientOptions { HandleCookies = false });

        var (response, problem) = await visitor
            .POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(new()
            {
                Username = NoMembershipUsername,
                Password = TestUsers.DefaultPassword
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "an account that belongs to no active tenant is turned away at the door rather than signed in to a session in which nothing works");
        problem.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ErrorCodes.TenantRequired);

        // And naming a tenant it does not belong to does not get it in either: the refusal names the
        // standing it lacks rather than the field it left empty.
        var identifier = await TenantIdentifierOfAsync(TestTenants.BootstrapTenantId);

        var (named, namedProblem) = await visitor
            .POSTAsync<TokenEndpoint, TokenRequest, ProblemDetails>(new()
            {
                Username = NoMembershipUsername,
                Password = TestUsers.DefaultPassword,
                TenantIdentifier = identifier
            });

        named.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        namedProblem.Errors.Should().ContainSingle()
            .Which.Code.Should().Be(ErrorCodes.NotTenantMember,
                "membership is what places an account inside a tenant, and this one holds none anywhere");
    }
}