namespace Backend.Tests.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the <see cref="UserDeleteEndpoint"/> covering deletion of users, non-existent users,
/// protected system-created users, and the account of another tenant that is out of reach (AC-094).
/// </summary>
public class UserDeleteTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a created user can be successfully deleted and subsequent GET returns 404.
    /// </summary>
    [Fact]
    public async Task Delete_User()
    {
        await SetAuthTokenAsync();

        var roleService = Service<IRoleService>();
        var faker = new Faker<UserCreateRequest>()
                    .RuleFor(u => u.Username, f => f.Internet.UserName() + f.UniqueIndex)
                    .RuleFor(u => u.Email, f => f.Internet.Email() + f.UniqueIndex)
                    .RuleFor(u => u.Password, f => f.Internet.Password())
                    .RuleFor(u => u.FirstName, f => f.Name.FirstName())
                    .RuleFor(u => u.LastName, f => f.Name.LastName())
                    .RuleFor(u => u.IsActive, f => true);
        var request = faker.Generate();
        request.Roles = [TestRoles.TestRoleId];
        var (createRsp, createRes) = await Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(request);

        createRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then delete the user
        var (deleteRsp, deleteRes) = await Client.DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, UserDeleteResponse>(
            new()
            {
                Id = createRes.Id
            });

        deleteRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        deleteRes.Success.Should().BeTrue();

        // Verify user is deleted by trying to get it
        var (getRsp, _) = await Client.GETAsync<UserGetEndpoint, UserGetRequest, UserGetResponse>(
            new()
            {
                Id = createRes.Id
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that deleting a non-existent user returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Delete_NonExistent_User()
    {
        await SetAuthTokenAsync();

        var (deleteRsp, _) = await Client.DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, UserDeleteResponse>(
            new()
            {
                Id = Guid.NewGuid()
            });

        deleteRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that attempting to delete the system-created admin user returns 400 Bad Request with <see cref="ErrorCodes.SystemCreatedUserCannotBeDeleted"/>.
    /// </summary>
    [Fact]
    public async Task Cannot_Delete_Admin_User()
    {
        await SetAuthTokenAsync();

        // Get the system-created user (admin from seeder)
        var userService = Service<IUserService>();
        var systemCreatedUser = await userService.GetByUsernameAsync(TestUsers.TenantAdminUsername);
        systemCreatedUser.Should().NotBeNull();

        // Try to delete the system-created user
        var (deleteRsp, res) = await Client.DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, ProblemDetails>(
            new()
            {
                Id = systemCreatedUser!.Id
            });

        deleteRsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Should().ContainSingle();
        res.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedUserCannotBeDeleted);
    }

    /// <summary>
    /// Verifies that deleting an account holding no membership in the tenant being acted in answers
    /// exactly as deleting an account that does not exist does, and leaves it in place (AC-094).
    /// </summary>
    /// <remarks>
    /// The account is read back afterwards, both to say it is still there and to say nothing about it
    /// changed: a delete that had reached the row and been refused later would leave a soft-deleted
    /// account whose absence the next read would report as a missing one. It also still belongs to its
    /// own tenant and is still administered from there, which is what makes "left in place" mean the
    /// account rather than only its row.
    /// </remarks>
    [Fact]
    public async Task Cross_Tenant_User_Responds_As_Missing()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            acted.Id, await CreateTenantRoleAsync(acted.Id, Allow.User_Delete));
        var stranger = await CreateTenantUserAsync(
            other.Id, await CreateTenantRoleAsync(other.Id, Allow.User_View));

        var stored = await DbContext.Users
            .AsNoTracking()
            .SingleAsync(account => account.Id == stranger.Id, TestContext.Current.CancellationToken);
        var before = (stored.Username, stored.Email, stored.FirstName, stored.LastName, stored.IsActive);

        var client = await ClientForAsync(administrator.Username);

        var (refused, _) = await client
            .DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, UserDeleteResponse>(new() { Id = stranger.Id });

        var (unknown, _) = await client
            .DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, UserDeleteResponse>(new() { Id = Guid.NewGuid() });

        refused.StatusCode.Should().Be(unknown.StatusCode,
            "an account of another tenant and an account that never existed are one answer, not two");
        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var refusedBody = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var unknownBody = await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        refusedBody.Should().Be(unknownBody,
            "nothing in the answer distinguishes an account the caller may not administer from one that is not there");

        var after = await DbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(account => account.Id == stranger.Id, TestContext.Current.CancellationToken);
        after.Should().NotBeNull("the account belongs to its own tenant and was left exactly where it was");
        (after!.Username, after.Email, after.FirstName, after.LastName, after.IsActive).Should().Be(before,
            "nothing about the account of another tenant was changed on the way to refusing the request");
    }

    /// <summary>
    /// Verifies that an account belonging to another tenant as well cannot be deleted from inside this
    /// one: deleting an account ends it everywhere, so the tenant that shares it would lose a member it
    /// never gave up. The refusal names the shared standing and the account survives.
    /// </summary>
    [Fact]
    public async Task Account_Shared_With_Another_Tenant_Cannot_Be_Deleted()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            acted.Id, await CreateTenantRoleAsync(acted.Id, Allow.User_Delete));
        var shared = await CreateTenantUserAsync(acted.Id);
        var cancellationToken = TestContext.Current.CancellationToken;

        await MembershipService.AddAsync(other.Id, shared.Id, [], cancellationToken);

        var client = await ClientForAsync(administrator.Username, acted.Id);

        var (response, problem) = await client.DELETEAsync<UserDeleteEndpoint, UserDeleteRequest, ProblemDetails>(
            new() { Id = shared.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.UserSharedAcrossTenants);

        (await DbContext.Users.AsNoTracking().AnyAsync(account => account.Id == shared.Id, cancellationToken))
            .Should().BeTrue("the account the other tenant also relies on is left in place");
    }
}