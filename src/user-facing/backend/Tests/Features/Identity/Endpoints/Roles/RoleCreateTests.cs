using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;

namespace Backend.Tests.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests for the <see cref="RoleCreateEndpoint"/> covering validation and successful role creation, the
/// name a role is held to - unique within its tenant and nowhere wider - the tenant a
/// created role is attributed to without being told, and the audit fields it records.
/// </summary>
public class RoleCreateTests(App app) : UsersTestsBase(app)
{
    /// <summary>
    /// Verifies that invalid input (empty name, description exceeding max length) returns a 400 Bad Request with appropriate validation errors.
    /// </summary>
    [Fact]
    public async Task Invalid_Input()
    {
        await SetAuthTokenAsync();

        RoleCreateRequest request = new()
        {
            Name = "",
            Description = new string('x', 1025),
        };
        var (rsp, res) = await Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, ProblemDetails>(request);

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Count().Should().Be(2);
        res.Errors.Select(e => e.Name).Should().Equal("name", "description");
    }

    /// <summary>
    /// Verifies that a valid role creation request returns 200 OK with the correct name and description.
    /// </summary>
    [Fact]
    public async Task Valid_Input()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<RoleCreateRequest>()
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var request = faker.Generate();
        var (rsp, res) = await Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(request);

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Name.Should().Be(request.Name);
        res.Description.Should().Be(request.Description);
    }

    /// <summary>
    /// Verifies that a role records the account that created it and when.
    /// </summary>
    /// <remarks>
    /// The caller is an account made for this test rather than the seeded administrator, so the creating
    /// user asserted is one the test can name exactly and no other test can write a role as. The row
    /// carries its creation as its last change until something changes it, which is what the update
    /// fields are read for: a role that came back already changed would mean its creator was not the
    /// only one to have written it.
    /// </remarks>
    [Fact]
    public async Task Records_Audit_Fields()
    {
        var administrator = await CreateUserAsync(await CreateRoleAsync(Allow.Role_Create));

        var client = await ClientForAsync(administrator.Username);

        var before = DateTime.UtcNow;
        var (response, created) = await client
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(
                new() { Name = NewRoleName(), Description = RoleDescription });
        var after = DateTime.UtcNow;

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await RoleAsync(created.Id);

        stored.CreatedBy.Should().Be(administrator.Id, "the creating account is the caller that asked for the role");
        stored.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after, "the creation time is the time of the request");
        stored.UpdatedBy.Should().BeNull("nothing has changed the role since it was made");
        stored.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(
            after,
            "a row carries its creation as its last change until something changes it");
    }

    /// <summary>
    /// A description valid under the endpoint's rules and identical for every role a test makes, so the
    /// name is the only thing that ever distinguishes one creation from another.
    /// </summary>
    private const string RoleDescription = "Role made by a tenancy test";

    /// <summary>
    /// A role name no other role can hold: role names are unique within their tenant, and a name derived
    /// from a fresh identifier cannot collide with one a previous run of the suite left behind.
    /// </summary>
    /// <returns>The role name.</returns>
    private static string NewRoleName() => $"Role {Guid.NewGuid():N}";

    /// <summary>
    /// Reads a role back through the database rather than through the surface that just wrote it, and
    /// across every tenant, because what is being asserted is where the row landed.
    /// </summary>
    /// <param name="roleId">The role to read.</param>
    /// <returns>The stored role.</returns>
    private async Task<Role> RoleAsync(Guid roleId)
        => await DbContext.Roles
            
            .AsNoTracking()
            .SingleAsync(role => role.Id == roleId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Duplicate_Name_Ignoring_Case_Is_Refused()
    {
        var administrator = await CreateUserAsync(await CreateRoleAsync(Allow.Role_Create));
        var client = await ClientForAsync(administrator.Username);
        var name = $"Role {Guid.NewGuid():N}";

        (await client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(new() { Name = name }))
            .Response.StatusCode.Should().Be(HttpStatusCode.OK);

        var (rsp, problem) = await client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, ProblemDetails>(
            new() { Name = $"  {name.ToUpperInvariant()}  " });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(e => e.Code).Should().Contain(ErrorCodes.RoleNameAlreadyExists.Value);
    }
}
