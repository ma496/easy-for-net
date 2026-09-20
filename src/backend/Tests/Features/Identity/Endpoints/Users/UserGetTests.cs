using Backend.Features.Identity.Core;

namespace Backend.Tests.Features.Identity.Endpoints.Users;

using Backend.Features.Identity.Endpoints.Users;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the <see cref="UserGetEndpoint"/> covering retrieval of existing and non-existent users,
/// and the account of another tenant that is not there to be read (AC-094).
/// </summary>
public class UserGetTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a created user can be retrieved by ID with the correct username and email.
    /// </summary>
    [Fact]
    public async Task Get_User()
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

        var (getRsp, getRes) = await Client.GETAsync<UserGetEndpoint, UserGetRequest, UserGetResponse>(
            new()
            {
                Id = createRes.Id
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        getRes.Username.Should().Be(request.Username);
        getRes.Email.Should().Be(request.Email);
        getRes.SystemCreated.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a user created by the seeder is reported as system-created so the UI can hide its actions.
    /// </summary>
    [Fact]
    public async Task Get_SystemCreated_User()
    {
        await SetAuthTokenAsync();

        var (getRsp, getRes) = await Client.GETAsync<UserGetEndpoint, UserGetRequest, UserGetResponse>(
            new()
            {
                Id = TestUsers.TenantAdminUserId
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        getRes.SystemCreated.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that requesting a non-existent user returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Get_NonExistent_User()
    {
        await SetAuthTokenAsync();

        var (getRsp, _) = await Client.GETAsync<UserGetEndpoint, UserGetRequest, UserGetResponse>(
            new()
            {
                Id = Guid.NewGuid()
            });

        getRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that reading an account holding no membership in the tenant being acted in answers
    /// exactly as reading an account that does not exist does, and leaves it untouched (AC-094).
    /// </summary>
    /// <remarks>
    /// The two answers are compared as one: the same status and the same body, so not even their shape
    /// tells a caller whether the account it named belongs to another tenant or to nobody. The
    /// account's stored values are compared as well, because a refusal that had read the row on its
    /// way to being refused would be a different answer wearing the same costume.
    /// </remarks>
    [Fact]
    public async Task Cross_Tenant_User_Responds_As_Missing()
    {
        var acted = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            acted.Id, await CreateTenantRoleAsync(acted.Id, Allow.User_View));
        var stranger = await CreateTenantUserAsync(
            other.Id, await CreateTenantRoleAsync(other.Id, Allow.User_View));

        var stored = await DbContext.Users
            .AsNoTracking()
            .SingleAsync(account => account.Id == stranger.Id, TestContext.Current.CancellationToken);
        var before = (stored.Username, stored.Email, stored.FirstName, stored.LastName, stored.IsActive);

        var client = await ClientForAsync(administrator.Username);

        var (refused, _) = await client
            .GETAsync<UserGetEndpoint, UserGetRequest, UserGetResponse>(new() { Id = stranger.Id });

        var (unknown, _) = await client
            .GETAsync<UserGetEndpoint, UserGetRequest, UserGetResponse>(new() { Id = Guid.NewGuid() });

        refused.StatusCode.Should().Be(unknown.StatusCode,
            "an account of another tenant and an account that never existed are one answer, not two");
        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var refusedBody = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var unknownBody = await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        refusedBody.Should().Be(unknownBody,
            "nothing in the answer distinguishes an account the caller may not administer from one that is not there");
        refusedBody.Should().NotContain(stranger.Username,
            "so that the account named by the caller is not confirmed back to them by being refused");

        var after = await DbContext.Users
            .AsNoTracking()
            .SingleAsync(account => account.Id == stranger.Id, TestContext.Current.CancellationToken);
        (after.Username, after.Email, after.FirstName, after.LastName, after.IsActive).Should().Be(before,
            "the account of another tenant was not read, and is exactly as it was");
    }
}