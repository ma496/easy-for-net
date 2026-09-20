namespace Backend.Tests.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the <see cref="UserUpdateEndpoint"/> covering updating users, non-existent users,
/// protected system-created users, and the account of another tenant that is out of reach (AC-094,
/// AC-105).
/// </summary>
public class UserUpdateTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a created user can be successfully updated with new first name, last name, active status, and roles.
    /// </summary>
    [Fact]
    public async Task Update_User()
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

        var updateFaker = new Faker<UserUpdateRequest>()
            .RuleFor(u => u.Id, f => createRes.Id)
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.IsActive, f => false);
        var updateRequest = updateFaker.Generate();
        updateRequest.Roles = [TestRoles.TestOneRoleId, TestRoles.TestTwoRoleId];
        var (updateRsp, updateRes) = await Client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(updateRequest);

        updateRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        updateRes.FirstName.Should().Be(updateRequest.FirstName);
        updateRes.LastName.Should().Be(updateRequest.LastName);
        updateRes.IsActive.Should().Be(updateRequest.IsActive);
        updateRes.Roles.Should().Equal(updateRequest.Roles);
    }

    /// <summary>
    /// Verifies that updating a non-existent user returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Update_NonExistent_User()
    {
        await SetAuthTokenAsync();

        var roleService = Service<IRoleService>();
        var updateFaker = new Faker<UserUpdateRequest>()
            .RuleFor(u => u.Id, f => Guid.NewGuid())
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.IsActive, f => true);
        var updateRequest = updateFaker.Generate();
        updateRequest.Roles = [TestRoles.TestRoleId];
        var (updateRsp, _) = await Client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(updateRequest);

        updateRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that updating the system-created admin user returns 400 Bad Request with <see cref="ErrorCodes.SystemCreatedUserCannotBeUpdated"/>.
    /// </summary>
    [Fact]
    public async Task Update_Admin_User_Should_Fail()
    {
        await SetAuthTokenAsync();

        // Get the system-created user
        var systemCreatedUser = await Service<IUserService>()
            .Users()
            .FirstAsync(u => u.Username == TestUsers.TenantAdminUsername, cancellationToken: TestContext.Current.CancellationToken);

        var roleService = Service<IRoleService>();
        var (updateRsp, res) = await Client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, ProblemDetails>(
            new()
            {
                Id = systemCreatedUser.Id,
                FirstName = "Modified",
                LastName = "Default",
                IsActive = false,
                Roles = [TestRoles.TestRoleId]
            });

        updateRsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Should().ContainSingle();
        res.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedUserCannotBeUpdated);
    }

    /// <summary>
    /// Verifies that updating an account holding no membership in the tenant being acted in answers
    /// exactly as updating an account that does not exist does, and leaves it untouched (AC-094,
    /// AC-105).
    /// </summary>
    /// <remarks>
    /// The request asks for a change to the account's profile and for it to be deactivated, so the
    /// stored values compared afterwards say whether any part of it was applied: an account reached by
    /// an administrator of another tenant would come back renamed, and - the half AC-105 speaks to -
    /// an account whose active flag was reached from outside its tenant would be one deactivation away
    /// from losing the access it was meant to keep.
    /// </remarks>
    [Fact]
    public async Task Cross_Tenant_User_Responds_As_Missing()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var actedRoleId = await CreateTenantRoleAsync(acted.Id, Allow.User_View, Allow.User_Update);
        var administrator = await CreateTenantUserAsync(acted.Id, actedRoleId);
        var stranger = await CreateTenantUserAsync(
            other.Id, await CreateTenantRoleAsync(other.Id, Allow.User_View));

        var stored = await DbContext.Users
            .AsNoTracking()
            .SingleAsync(account => account.Id == stranger.Id, TestContext.Current.CancellationToken);
        var before = (stored.Username, stored.Email, stored.FirstName, stored.LastName, stored.IsActive);

        var client = await ClientForAsync(administrator.Username);

        var (refused, _) = await client
            .PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(new()
            {
                Id = stranger.Id,
                FirstName = "Changed",
                LastName = "Changed",
                IsActive = false,
                Roles = [actedRoleId]
            });

        var (unknown, _) = await client
            .PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(new()
            {
                Id = Guid.NewGuid(),
                FirstName = "Changed",
                LastName = "Changed",
                IsActive = false,
                Roles = [actedRoleId]
            });

        refused.StatusCode.Should().Be(unknown.StatusCode,
            "an account of another tenant and an account that never existed are one answer, not two");
        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var refusedBody = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var unknownBody = await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        refusedBody.Should().Be(unknownBody,
            "nothing in the answer distinguishes an account the caller may not administer from one that is not there");

        var after = await DbContext.Users
            .AsNoTracking()
            .SingleAsync(account => account.Id == stranger.Id, TestContext.Current.CancellationToken);
        after.IsActive.Should().BeTrue("the request asked for the account to be deactivated from outside its tenant, and was refused before reaching it");
        (after.Username, after.Email, after.FirstName, after.LastName, after.IsActive).Should().Be(before,
            "no part of the update was applied to the account of another tenant");
    }

    /// <summary>
    /// Verifies that an account belonging to another tenant as well is not one tenant's to change:
    /// the refusal names the shared standing, and the account is left active. An account is one
    /// identity across every tenant it belongs to, so deactivating it here would lock it out of the
    /// other tenant too - and anybody can reach this standing unaided, by creating a tenant of their
    /// own and adding somebody else's member to it.
    /// </summary>
    [Fact]
    public async Task Account_Shared_With_Another_Tenant_Cannot_Be_Updated()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            acted.Id, await CreateTenantRoleAsync(acted.Id, Allow.User_Update));
        var ordinaryRoleId = await CreateTenantRoleAsync(acted.Id, Allow.User_View);
        var shared = await CreateTenantUserAsync(acted.Id, ordinaryRoleId);
        var cancellationToken = TestContext.Current.CancellationToken;

        await MembershipService.AddAsync(other.Id, shared.Id, [], cancellationToken);

        var client = await ClientForAsync(administrator.Username, acted.Id);

        var (response, problem) = await client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, ProblemDetails>(
            new() { Id = shared.Id, FirstName = "Locked", LastName = "Out", IsActive = false, Roles = [ordinaryRoleId] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.UserSharedAcrossTenants);

        var stored = await DbContext.Users
            .AsNoTracking()
            .SingleAsync(account => account.Id == shared.Id, cancellationToken);

        stored.IsActive.Should().BeTrue("nothing of the request was applied, so the account is still usable in the tenant it also belongs to");
        stored.FirstName.Should().NotBe("Locked");
    }

    /// <summary>
    /// Verifies the other half of that rule: an account that belongs to this tenant alone is
    /// administered here exactly as before, so the refusal above is about the account being shared
    /// rather than about the surface being closed.
    /// </summary>
    [Fact]
    public async Task Account_Of_This_Tenant_Alone_Is_Still_Updated()
    {
        var acted = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            acted.Id, await CreateTenantRoleAsync(acted.Id, Allow.User_Update));
        var ordinaryRoleId = await CreateTenantRoleAsync(acted.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(acted.Id, ordinaryRoleId);

        var client = await ClientForAsync(administrator.Username, acted.Id);

        var (response, updated) = await client.PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(
            new() { Id = member.Id, FirstName = "Still", LastName = "Administered", IsActive = true, Roles = [ordinaryRoleId] });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.FirstName.Should().Be("Still");
    }
}