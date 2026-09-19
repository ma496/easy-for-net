namespace Backend.Tests.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Tests.Features.Tenancy;
using Backend.Tests.Seeder;

/// <summary>
/// Tests for the <see cref="UserCreateEndpoint"/> covering validation and successful user creation,
/// the sign-in identifiers that are unique across the whole platform (AC-048), the membership an
/// account created inside a tenant is granted in it (AC-096), and the account tier that follows the
/// scope it was created in.
/// </summary>
public class UserCreateTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that invalid input returns a 400 Bad Request with validation errors for all required fields.
    /// </summary>
    [Fact]
    public async Task Invalid_Input()
    {
        await SetAuthTokenAsync();

        var request = new UserCreateRequest
        {
            Username = "a",
            Email = "invalid-email",
            Password = "123",
            FirstName = "a",
            LastName = "a",
            IsActive = true
        };
        var (rsp, res) = await App.Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(request);

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Count().Should().Be(6);
        res.Errors.Select(e => e.Name).Should().Equal("username", "email", "password", "firstName", "lastName", "roles");
    }

    /// <summary>
    /// Verifies that a valid user creation request returns 200 OK with the correct user details and assigned roles.
    /// </summary>
    [Fact]
    public async Task Valid_Input()
    {
        await SetAuthTokenAsync();

        var roleService = App.Services.GetRequiredService<IRoleService>();
        var faker = new Faker<UserCreateRequest>()
            .RuleFor(u => u.Username, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Email, f => f.Internet.Email() + f.UniqueIndex)
            .RuleFor(u => u.Password, f => f.Internet.Password())
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.IsActive, f => true);
        var request = faker.Generate();
        request.Roles = [TestRoles.TestRoleId];
        var (rsp, res) = await App.Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Username.Should().Be(request.Username);
        res.Email.Should().Be(request.Email);
        res.FirstName.Should().Be(request.FirstName);
        res.LastName.Should().Be(request.LastName);
        res.IsActive.Should().Be(request.IsActive);
        res.Roles.Should().Equal(request.Roles);
    }

    /// <summary>
    /// Verifies that a username or an email address already taken is refused even when the account
    /// holding it belongs to a tenant the caller does not administer (AC-048).
    /// </summary>
    /// <remarks>
    /// The two administrators act in different tenants and neither is a member of the other's, so the
    /// second one cannot see the first one's account on any list of its own and still cannot create a
    /// second account under the same sign-in identifier. Granting it would produce two accounts that
    /// sign-in could not tell apart, since sign-in names no tenant to choose between them by (AC-049).
    /// </remarks>
    [Fact]
    public async Task Username_And_Email_Stay_Globally_Unique()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var firstRoleId = await CreateTenantRoleAsync(first.Id, Allow.User_Create);
        var secondRoleId = await CreateTenantRoleAsync(second.Id, Allow.User_Create);
        var firstAdministrator = await CreateTenantUserAsync(first.Id, firstRoleId);
        var secondAdministrator = await CreateTenantUserAsync(second.Id, secondRoleId);

        var username = NewUsername();
        var email = EmailOf(username);

        var firstClient = await ClientForAsync(firstAdministrator.Username);
        var (created, _) = await firstClient
            .POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(new()
            {
                Username = username,
                Email = email,
                Password = TestUsers.DefaultPassword,
                IsActive = true,
                Roles = [firstRoleId]
            });

        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondClient = await ClientForAsync(secondAdministrator.Username);

        var (reusedUsername, usernameRefusal) = await secondClient
            .POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(new()
            {
                Username = username,
                Email = EmailOf(NewUsername()),
                Password = TestUsers.DefaultPassword,
                IsActive = true,
                Roles = [secondRoleId]
            });

        reusedUsername.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a name taken in another tenant is taken here, because one person authenticates with one account however many tenants they belong to");
        usernameRefusal.Errors.Should().ContainSingle();
        usernameRefusal.Errors.First().Code.Should().Be(ErrorCodes.UsernameAlreadyExists);

        var attemptedUsername = NewUsername();
        var (reusedEmail, emailRefusal) = await secondClient
            .POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(new()
            {
                Username = attemptedUsername,
                Email = email,
                Password = TestUsers.DefaultPassword,
                IsActive = true,
                Roles = [secondRoleId]
            });

        reusedEmail.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        emailRefusal.Errors.Should().ContainSingle();
        emailRefusal.Errors.First().Code.Should().Be(ErrorCodes.EmailAlreadyExists);

        // Refused before anything was written: the address in the second attempt is the collision, and
        // the account it would otherwise have created is not there under the name it named.
        (await DbContext.Users
                .AsNoTracking()
                .AnyAsync(account => account.UsernameNormalized == attemptedUsername.ToLowerInvariant(), TestContext.Current.CancellationToken))
            .Should().BeFalse("the refusal is the whole outcome of the request, not a report made after a partial write");
    }

    /// <summary>
    /// Verifies that an account created by a caller acting in a tenant is made a member of that tenant
    /// and is visible there afterwards (AC-096).
    /// </summary>
    /// <remarks>
    /// The membership is asserted from the database rather than from the response, and the account is
    /// then searched for on the tenant's own list: the two together say the account is not merely
    /// recorded as a member but is in the set an administrator of that tenant administers. The
    /// membership is written from the tenant being acted in, so nothing the request carries could place
    /// the account anywhere else - which the exact set asserted here would catch.
    /// </remarks>
    [Fact]
    public async Task Created_User_Gains_A_Membership_In_The_Active_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_Create, Allow.User_View);
        var administrator = await CreateTenantUserAsync(tenant.Id, roleId);

        var client = await ClientForAsync(administrator.Username);
        var username = NewUsername();

        var (response, created) = await client
            .POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(new()
            {
                Username = username,
                Email = EmailOf(username),
                Password = TestUsers.DefaultPassword,
                IsActive = true,
                Roles = [roleId]
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var memberships = await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(membership => membership.UserId == created.Id)
            .Select(membership => membership.TenantId)
            .ToListAsync(TestContext.Current.CancellationToken);

        memberships.Should().Equal([tenant.Id],
            "the account was created by a caller acting in this tenant, so it holds exactly one membership and it is in that tenant");

        var (listed, page) = await client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
                new() { Page = 1, PageSize = 10, Search = username });

        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Items.Select(item => item.Id).Should().Equal([created.Id],
            "the account is in the set the tenant's own administrator administers, which is what makes the membership of use to the caller that wrote it");
    }

    /// <summary>
    /// Verifies that the tier a new account gets follows the scope it was created in: created while
    /// acting in no tenant it is one of the platform's own accounts and joins no tenant, and created
    /// inside a tenant - by a platform account that entered one just as by that tenant's own
    /// administrator - it is an ordinary account of that tenant.
    /// </summary>
    /// <remarks>
    /// The same caller creates both accounts, which is the whole point: the tier follows the scope the
    /// request runs in and never the caller who made it, so nothing a platform account does inside a
    /// tenant can mint another platform account there. Both halves are asserted from the stored row,
    /// because the tier is not something the request carries or the response echoes.
    /// </remarks>
    [Fact]
    public async Task Created_Accounts_Take_Their_Tier_From_The_Scope_They_Are_Created_In()
    {
        var tenant = await CreateTenantAsync();
        var tenantRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_Create);

        await SetPlatformAdminAuthTokenAsync();

        var platformUsername = NewUsername();
        var (platformResponse, platformAccount) = await App.Client
            .POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(new()
            {
                Username = platformUsername,
                Email = EmailOf(platformUsername),
                Password = TestUsers.DefaultPassword,
                IsActive = true,
                Roles = [TestRoles.PlatformAdminRoleId]
            });

        platformResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await TierOfAsync(platformAccount.Id)).Should().BeTrue(
            "creating an account while acting in no tenant is how the platform's own accounts come into being");
        (await MembershipTenantIdsAsync(platformAccount.Id)).Should().BeEmpty(
            "and it belongs to no tenant, which is what platform scope means");

        // The very same caller, now inside a tenant it holds no membership of.
        await SwitchTenantAsync(tenant.Id);

        var tenantUsername = NewUsername();
        var (tenantResponse, tenantAccount) = await App.Client
            .POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(new()
            {
                Username = tenantUsername,
                Email = EmailOf(tenantUsername),
                Password = TestUsers.DefaultPassword,
                IsActive = true,
                Roles = [tenantRoleId]
            });

        tenantResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await TierOfAsync(tenantAccount.Id)).Should().BeFalse(
            "an account created inside a tenant is that tenant's, whoever created it");
        (await MembershipTenantIdsAsync(tenantAccount.Id)).Should().Equal([tenant.Id],
            "and it joins the tenant it was created in, so the caller that wrote it can administer it next");
    }

    /// <summary>
    /// Whether a stored account belongs to the platform tier, read from the row rather than from
    /// anything the request or the response carried.
    /// </summary>
    /// <param name="userId">The account to read.</param>
    /// <returns>The account's tier.</returns>
    private async Task<bool> TierOfAsync(Guid userId)
        => await DbContext.Users
            .AsNoTracking()
            .Where(account => account.Id == userId)
            .Select(account => account.IsPlatform)
            .SingleAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// The tenants an account holds a membership in, read across every tenant because the question is
    /// which ones it landed in rather than what one of them can see.
    /// </summary>
    /// <param name="userId">The account to read.</param>
    /// <returns>The tenants it belongs to.</returns>
    private async Task<List<Guid?>> MembershipTenantIdsAsync(Guid userId)
        => await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Select(membership => membership.TenantId)
            .ToListAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// A username no other account holds and no run of the suite can collide with: account names are
    /// globally unique and the test database is neither wiped nor recreated between runs.
    /// </summary>
    /// <returns>The username.</returns>
    private static string NewUsername() => $"u{Guid.NewGuid():N}";

    /// <summary>
    /// The email address built from a username, in the shape every seeded and created account uses.
    /// </summary>
    /// <param name="username">The username the address belongs to.</param>
    /// <returns>The email address.</returns>
    private static string EmailOf(string username) => $"{username}@example.com";
}