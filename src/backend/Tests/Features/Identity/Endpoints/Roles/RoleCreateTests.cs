namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the <see cref="RoleCreateEndpoint"/> covering validation and successful role creation, the
/// name a role is held to - unique within its tenant and nowhere wider (AC-038, AC-039) - the tenant a
/// created role is attributed to without being told (AC-112), and the audit fields it records (AC-078).
/// </summary>
public class RoleCreateTests(App app) : TenancyTestsBase(app)
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
        var (rsp, res) = await App.Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, ProblemDetails>(request);

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
        var (rsp, res) = await App.Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(request);

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Name.Should().Be(request.Name);
        res.Description.Should().Be(request.Description);
    }

    /// <summary>
    /// Verifies that the same role name can be taken in each of two tenants, the roles landing in the
    /// tenant their creator was acting in (AC-038).
    /// </summary>
    /// <remarks>
    /// An administrator is made in each tenant and neither is a member of the other's, so the two
    /// creations are as separate as two tenants can be. A name is only unique within its tenant, so the
    /// second creation is not a collision and must not be refused; what tells them apart afterwards is
    /// the tenant each row carries, which is also what keeps the two roles from being each other's role
    /// to administer.
    /// </remarks>
    [Fact]
    public async Task Same_Role_Name_In_Two_Tenants()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        var firstAdministrator = await CreateTenantUserAsync(
            first.Id, await CreateTenantRoleAsync(first.Id, Allow.Role_Create));
        var secondAdministrator = await CreateTenantUserAsync(
            second.Id, await CreateTenantRoleAsync(second.Id, Allow.Role_Create));

        var name = NewRoleName();

        var (firstRsp, firstRole) = await (await ClientForAsync(firstAdministrator.Username))
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(
                new() { Name = name, Description = RoleDescription });

        firstRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (secondRsp, secondRole) = await (await ClientForAsync(secondAdministrator.Username))
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(
                new() { Name = name, Description = RoleDescription });

        secondRsp.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "a role name is unique within its tenant and nowhere wider, so a name another tenant has taken is free here");
        secondRole.Id.Should().NotBe(firstRole.Id, "the second creation is a role of its own, not one already made");

        var storedFirst = await RoleAsync(firstRole.Id);
        var storedSecond = await RoleAsync(secondRole.Id);

        storedFirst.TenantId.Should().Be(first.Id, "the first role belongs to the tenant its creator was acting in");
        storedSecond.TenantId.Should().Be(second.Id, "and the second to the tenant its own creator was acting in");
        storedFirst.NameNormalized.Should().Be(storedSecond.NameNormalized,
            "the two are held to the same comparison, which is what makes the same name two names rather than one");

        var normalized = name.ToLowerInvariant();
        storedFirst.NameNormalized.Should().Be(normalized);
        storedSecond.NameNormalized.Should().Be(normalized);
    }

    /// <summary>
    /// Verifies that a name already used within the same tenant is refused however it is cased or
    /// spaced, and that the refusal leaves the tenant with exactly the roles it had (AC-039).
    /// </summary>
    /// <remarks>
    /// The comparison is made on the normalized column, so the second request asks for the same name in
    /// a different casing and with the surrounding whitespace a form or an API client tends to leave on
    /// a value. The role set is compared as a whole rather than counted, so a refusal that had excluded
    /// a role from the set would be caught as well as one that had added a second role under the same
    /// name.
    /// </remarks>
    [Fact]
    public async Task Duplicate_Name_In_Same_Tenant_Ignoring_Case()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_Create));

        var client = await ClientForAsync(administrator.Username);
        var name = NewRoleName();

        var (created, _) = await client
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(
                new() { Name = name, Description = RoleDescription });
        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var before = await RoleIdsInAsync(tenant.Id);

        var (refused, problem) = await client
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, ProblemDetails>(
                new() { Name = $"  {name.ToUpperInvariant()}  ", Description = RoleDescription });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.RoleNameAlreadyExists);
        problem.Errors.First().Name.Should().Be("name", "the caller is told which field to change");

        (await RoleIdsInAsync(tenant.Id)).Should().BeEquivalentTo(before,
            "the refusal is the whole outcome of the request: the tenant holds exactly the roles it held before it");
    }

    /// <summary>
    /// Verifies that a role created by a caller acting in a tenant belongs to that tenant, which the
    /// caller never names (AC-112).
    /// </summary>
    /// <remarks>
    /// The request type is checked for a tenant of its own before the request is made: a payload that
    /// offered one would be a way to place a role in a tenant the caller is not acting in, and the
    /// stored row would have to be read as the caller's intention rather than as the active scope's.
    /// The attribution asserted afterwards is therefore one nothing in the request could have set.
    /// </remarks>
    [Fact]
    public async Task Role_Is_Attributed_To_The_Active_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_Create));

        typeof(RoleCreateRequest).GetProperty("TenantId").Should().BeNull(
            "the tenant a role lands in is the one its creator is acting in, so no payload carries a tenant to name another");

        var client = await ClientForAsync(administrator.Username);

        var (response, created) = await client
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(
                new() { Name = NewRoleName(), Description = RoleDescription });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await RoleAsync(created.Id)).TenantId.Should().Be(
            tenant.Id,
            "the role was attributed to the tenant the caller was acting in, which the request never named");
    }

    /// <summary>
    /// Verifies that a role records the account that created it and when (AC-078).
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
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantUserAsync(
            tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_Create));

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
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(role => role.Id == roleId, TestContext.Current.CancellationToken);

    /// <summary>
    /// The roles a tenant holds, read inside its own scope so that the tenant filter - rather than this
    /// query - is what selects them.
    /// </summary>
    /// <param name="tenantId">The tenant whose roles are wanted.</param>
    /// <returns>The identifiers of every role the tenant holds.</returns>
    private async Task<List<Guid>> RoleIdsInAsync(Guid tenantId)
    {
        using var tenantScope = TenantContext.BeginTenant(tenantId);

        return await DbContext.Roles
            .AsNoTracking()
            .Select(role => role.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
