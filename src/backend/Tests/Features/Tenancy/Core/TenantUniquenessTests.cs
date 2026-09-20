using Backend.Features.Tenancy.Core.Entities;

namespace Backend.Tests.Features.Tenancy.Core;

using Backend.Data.Entities;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;
using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for what a retained row still reserves - an identifier or a role name that was freed only by
/// deletion - and for the database constraints underneath those comparisons (AC-003, AC-039, AC-102,
/// AC-144, AC-147).
/// </summary>
/// <remarks>
/// <para>
/// Both kinds of uniqueness here are enforced twice on purpose: once by a comparison the creating
/// surface makes, so the caller gets a refusal attributed to the field they have to change, and once by
/// a database constraint that is deliberately not filtered on deletion, so two requests racing each
/// other cannot both get past the comparison and a name freed only by a soft delete stays reserved.
/// The tests below prove both halves - the refusal the caller sees, and the constraint that would
/// refuse it even if the comparison were skipped.
/// </para>
/// <para>
/// The identifier case is the one that cannot be arranged through the create endpoint: its validator
/// admits lower-case identifiers only (AC-101), so a padded or mixed-case identifier has no way in
/// through HTTP and is created through the service instead - which is the same creation path the
/// endpoint delegates to. The refusal is then proved by submitting the lower-case form over HTTP, so
/// the comparison is shown to run against the normalized column rather than the stored text.
/// </para>
/// </remarks>
public class TenantUniquenessTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The soft-delete query filter's registered key, named so the reads below relax that one filter and
    /// leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// The descriptor the database reports a violated unique constraint under. Read off the driver's
    /// own message rather than through its exception type, so the test project keeps no direct
    /// dependency on the PostgreSQL driver.
    /// </summary>
    private const string IdentifierConstraint = "IX_Tenants_Identifier";

    /// <summary>
    /// The descriptor behind a role name being unique within its tenant.
    /// </summary>
    private const string RoleNameConstraint = "IX_Roles_TenantId_Name";

    /// <summary>
    /// Verifies that deleting a tenant does not free its identifier: a tenant removed through the
    /// endpoint goes on holding the identifier it was created with, and a second tenant asking for it is
    /// refused against the identifier field with nothing persisted (AC-003).
    /// </summary>
    [Fact]
    public async Task Soft_Deleted_Identifier_Is_Still_Reserved()
    {
        var tenant = await CreateTenantAsync();
        var identifier = tenant.Identifier;

        await DeleteTenantAsync(tenant.Id);

        var (response, problem) = await Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, ProblemDetails>(new()
            {
                Name = $"Reissued {Faker.GlobalUniqueIndex}",
                Identifier = identifier
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantIdentifierAlreadyExists);
        problem.Errors.First().Name.Should().BeEquivalentTo("Identifier", "the refusal is attributed to the field the caller must change");

        // Exactly one tenant holds the identifier, and it is the deleted one: the refused request
        // persisted nothing, and the deleted tenant's row was never released.
        var holders = await TenantsIncludingDeletedAsync(identifier);
        holders.Should().ContainSingle();
        holders[0].Id.Should().Be(tenant.Id);
        holders[0].IsDeleted.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that deleting a role does not free its name within its tenant: the name stays reserved,
    /// so creating a role of that name afterwards is refused rather than granted (AC-039).
    /// </summary>
    [Fact]
    public async Task Deleted_Role_Name_Is_Still_Reserved()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantAdministratorAsync(tenant.Id);
        var name = NewRoleName();

        var created = await CreateRoleAsync(administrator, name);
        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleted = await DeleteRoleAsync(administrator, created.Id);
        deleted.Should().Be(HttpStatusCode.OK);

        var rolesBefore = await CountRolesAsync(tenant.Id);

        var (response, problem) = await administrator
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, ProblemDetails>(new() { Name = name });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle();
        problem.Errors.First().Code.Should().Be(ErrorCodes.RoleNameAlreadyExists);
        problem.Errors.First().Name.Should().BeEquivalentTo("Name");

        (await CountRolesAsync(tenant.Id)).Should().Be(rolesBefore, "the refused request added no role, not even a deleted one");
    }

    /// <summary>
    /// Verifies that a tenant identifier is stored as entered but trimmed, with a lower-case normalized
    /// copy maintained beside it, and that the uniqueness comparison runs against that normalized copy
    /// rather than the stored text (AC-102).
    /// </summary>
    [Fact]
    public async Task Identifier_Is_Stored_Trimmed_With_A_Normalized_Copy()
    {
        var asEntered = $"Mixed-Case-{Guid.NewGuid():N}";
        var normalized = asEntered.ToLowerInvariant();

        Tenant created;
        using (TenantContext.BeginPlatformScope())
        {
            // The padded, mixed-case identifier goes in through the creation path itself, because the
            // create endpoint's validator admits lower-case identifiers only and this is the value that
            // has to reach the columns.
            created = await TenantService.CreateAsync(new Tenant
            {
                Name = "  Mixed Case Tenant  ",
                Identifier = $"  {asEntered}  "
            }, cancellationToken: TestContext.Current.CancellationToken);
        }

        created.Identifier.Should().Be(asEntered, "surrounding white-space is trimmed, and the case is kept");
        created.IdentifierNormalized.Should().Be(normalized);

        var stored = await DbContext.Tenants
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == created.Id, TestContext.Current.CancellationToken);

        stored.Identifier.Should().Be(asEntered);
        stored.IdentifierNormalized.Should().Be(normalized, "the normalized copy is what uniqueness and lookup compare");

        // The comparison is proved by asking in a form the stored text does not literally contain: the
        // lower-case identifier is refused even though the trimmed, mixed-case original is nothing a
        // string comparison would match it against.
        await SetPlatformAdminAuthTokenAsync();
        var (response, problem) = await Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, ProblemDetails>(new()
            {
                Name = $"Duplicate Case {Faker.GlobalUniqueIndex}",
                Identifier = normalized
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantIdentifierAlreadyExists);
    }

    /// <summary>
    /// Verifies that the uniqueness of a retained identifier and a retained role name is enforced by the
    /// database itself, not only by the comparisons the services make: a row inserted directly, past
    /// every service, is refused by the constraint (AC-144).
    /// </summary>
    [Fact]
    public async Task Uniqueness_Is_Enforced_By_The_Database_Over_Retained_Rows()
    {
        var tenant = await CreateTenantAsync();
        var roleName = NewRoleName();

        // Arranged before the tenant is retired, so the retained role row belongs to a tenant that was
        // live when it was written - the state a deleted tenant's own history is really in.
        await ArrangeDeletedRoleAsync(tenant.Id, roleName);
        await DeleteTenantAsync(tenant.Id);

        var duplicateTenant = await RefuseDuplicateTenantAsync(tenant.Identifier);
        duplicateTenant.ReportedConstraints.Should().Contain(IdentifierConstraint,
                                                             "the unique index over the normalized identifier is what reserves it");
        DbContext.Entry(duplicateTenant.Row).State = EntityState.Detached;

        var duplicateRole = await RefuseDuplicateRoleAsync(tenant.Id, roleName);
        duplicateRole.ReportedConstraints.Should().Contain(RoleNameConstraint,
                                                           "the per-tenant unique index over the normalized name is what reserves it");
        DbContext.Entry(duplicateRole.Row).State = EntityState.Detached;

        // The delete really did soft-delete rather than erase: the refusals above rest on the original
        // rows being retained, and a hard delete would have freed both names.
        (await TenantsIncludingDeletedAsync(tenant.Identifier)).Should().ContainSingle();
        (await RolesIncludingDeletedAsync(tenant.Id, roleName)).Should().ContainSingle();
    }

    /// <summary>
    /// Verifies that the per-tenant unique index treats two absent tenants as equal, so platform-scoped
    /// roles - which carry no tenant at all - are unique among themselves instead of escaping the
    /// constraint because a null never equals another null (AC-144).
    /// </summary>
    /// <remarks>
    /// This is the one place a comparison over the tenant column can be checked for the null case, and
    /// the behaviour is a property of the index rather than of any C# code: without
    /// <c>NULLS NOT DISTINCT</c> PostgreSQL would accept both rows and the platform would end up with two
    /// roles of the same name, only one of which any lookup would ever find.
    /// </remarks>
    [Fact]
    public async Task Platform_Role_Names_Are_Unique()
    {
        var name = $"platform-{Guid.NewGuid():N}";

        Role first;
        using (var platformScope = TenantContext.BeginPlatformScope())
        {
            first = new Role { SystemCreated = true, Name = name, Description = "Platform role arranged by a uniqueness test" };
            DbContext.Roles.Add(first);
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            var second = new Role { SystemCreated = true, Name = name, Description = "Platform role arranged by a uniqueness test" };
            DbContext.Roles.Add(second);

            var saved = async () => await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            var refused = await saved.Should().ThrowAsync<DbUpdateException>("two platform roles of one name must not both exist");

            ConstraintReportedFor(refused.Which).Should().Contain(RoleNameConstraint);
            DbContext.Entry(second).State = EntityState.Detached;

            first.TenantId.Should().BeNull("a role with no tenant is what this case is about");

            // Soft-deleted rather than left standing: a live platform role of this test's own name would
            // outlast the test for no reason.
            DbContext.Roles.Remove(first);
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Verifies that both retained names are refused as duplicates when they are submitted again through
    /// the surfaces that own them - the deleted tenant's identifier and the deleted tenant role's name
    /// (AC-147).
    /// </summary>
    [Fact]
    public async Task Deleted_Names_Are_Refused_As_Duplicates()
    {
        var tenant = await CreateTenantAsync();
        var administrator = await CreateTenantAdministratorAsync(tenant.Id);
        var roleName = NewRoleName();

        var role = await CreateRoleAsync(administrator, roleName);
        (await DeleteRoleAsync(administrator, role.Id)).Should().Be(HttpStatusCode.OK);

        // The role half is asked first, while the tenant is still standing: retiring the tenant ends the
        // administrator's session in it, so the same request afterwards would be refused for the wrong
        // reason.
        var (nameResponse, nameProblem) = await administrator
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, ProblemDetails>(new() { Name = roleName });

        nameResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        nameProblem.Errors.First().Code.Should().Be(ErrorCodes.RoleNameAlreadyExists);

        await DeleteTenantAsync(tenant.Id);

        var (identifierResponse, identifierProblem) = await Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, ProblemDetails>(new()
            {
                Name = $"Reissued {Faker.GlobalUniqueIndex}",
                Identifier = tenant.Identifier
            });

        identifierResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        identifierProblem.Errors.First().Code.Should().Be(ErrorCodes.TenantIdentifierAlreadyExists);
    }

    /// <summary>
    /// Retires a tenant the way the platform surface does, so the identifier it leaves behind is
    /// reserved by a row that was really soft-deleted rather than by one this test wrote by hand.
    /// </summary>
    /// <param name="tenantId">The tenant to delete.</param>
    private async Task DeleteTenantAsync(Guid tenantId)
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, _) = await Client
            .DELETEAsync<TenantDeleteEndpoint, TenantDeleteRequest, TenantDeleteResponse>(new() { Id = tenantId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Creates an account inside a tenant holding a role that carries exactly the role-creating and
    /// role-deleting permissions, and returns a client presenting that account's session.
    /// </summary>
    /// <param name="tenantId">The tenant the administrator is to act in.</param>
    private async Task<HttpClient> CreateTenantAdministratorAsync(Guid tenantId)
    {
        var roleId = await CreateTenantRoleAsync(tenantId, Allow.Role_Create, Allow.Role_Delete);
        var administrator = await CreateTenantUserAsync(tenantId, roleId);
        return await ClientForAsync(administrator.Username, tenantId);
    }

    /// <summary>
    /// Creates a role through the endpoint whose name is unique to the calling test.
    /// </summary>
    /// <param name="client">The administrator creating the role.</param>
    /// <param name="name">The name the role is to carry.</param>
    private static async Task<RoleCreated> CreateRoleAsync(HttpClient client, string name)
    {
        var (response, created) = await client
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(new() { Name = name });

        return new RoleCreated(response.StatusCode, created.Id);
    }

    /// <summary>
    /// Deletes a role through the endpoint.
    /// </summary>
    /// <param name="client">The administrator deleting the role.</param>
    /// <param name="roleId">The role to delete.</param>
    private static async Task<HttpStatusCode> DeleteRoleAsync(HttpClient client, Guid roleId)
    {
        var (response, _) = await client
            .DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, RoleDeleteResponse>(new() { Id = roleId });

        return response.StatusCode;
    }

    /// <summary>
    /// Soft-deletes a role directly, so the retained row the constraint below has to refuse exists
    /// without a second account and a second endpoint call being arranged for it.
    /// </summary>
    /// <param name="tenantId">The tenant the role belongs to.</param>
    /// <param name="name">The name the role is to leave behind.</param>
    private async Task ArrangeDeletedRoleAsync(Guid tenantId, string name)
    {
        using var tenantScope = TenantContext.BeginTenant(tenantId);

        var role = new Role { SystemCreated = false, Name = name, Description = "Role arranged by a uniqueness test" };
        DbContext.Roles.Add(role);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Removing an ISoftDelete entity is turned into a soft delete by AppDbContext, which is exactly
        // the retained row whose name must go on being reserved.
        DbContext.Roles.Remove(role);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Inserts a tenant carrying an already-used normalized identifier, straight past the service that
    /// would have refused it, and returns the row together with what the database said about it - the
    /// row so the caller can drop it from the change tracker before the next save on this context.
    /// </summary>
    /// <param name="identifier">The identifier the incumbent tenant already holds.</param>
    private async Task<RefusedInsert<Tenant>> RefuseDuplicateTenantAsync(string identifier)
    {
        using var platformScope = TenantContext.BeginPlatformScope();

        var duplicate = new Tenant { Name = "Duplicate Identifier Tenant", Identifier = identifier };
        DbContext.Tenants.Add(duplicate);

        var saved = async () => await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var refused = await saved.Should().ThrowAsync<DbUpdateException>("the database, not just the comparison, has to hold the identifier");

        return new RefusedInsert<Tenant>(duplicate, ConstraintReportedFor(refused.Which));
    }

    /// <summary>
    /// Inserts a role re-using a name one of its tenant's deleted roles still holds, straight past the
    /// service that would have refused it.
    /// </summary>
    /// <param name="tenantId">The tenant the role is to be inserted into.</param>
    /// <param name="name">The name the tenant's deleted role still reserves.</param>
    private async Task<RefusedInsert<Role>> RefuseDuplicateRoleAsync(Guid tenantId, string name)
    {
        using var tenantScope = TenantContext.BeginTenant(tenantId);

        var duplicate = new Role { SystemCreated = false, Name = name, Description = "Role arranged by a uniqueness test" };
        DbContext.Roles.Add(duplicate);

        var saved = async () => await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var refused = await saved.Should().ThrowAsync<DbUpdateException>();

        return new RefusedInsert<Role>(duplicate, ConstraintReportedFor(refused.Which));
    }

    /// <summary>
    /// Every tenant holding an identifier, deleted ones included - the read that tells "the name is
    /// still reserved" apart from "the row was erased".
    /// </summary>
    /// <param name="identifier">The identifier being asked about, as it was stored.</param>
    private async Task<List<Tenant>> TenantsIncludingDeletedAsync(string identifier)
    {
        var normalized = identifier.Trim().ToLowerInvariant();

        return await DbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .Where(candidate => candidate.IdentifierNormalized == normalized)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Every role of a tenant holding a name, deleted ones included.
    /// </summary>
    /// <param name="tenantId">The tenant whose roles are read.</param>
    /// <param name="name">The name being asked about.</param>
    private async Task<List<Role>> RolesIncludingDeletedAsync(Guid tenantId, string name)
    {
        var normalized = name.Trim().ToLowerInvariant();

        return await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .Where(candidate => candidate.TenantId == tenantId && candidate.NameNormalized == normalized)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The number of roles one tenant holds, all of them and not only the live ones - the delta a
    /// refused creation must leave untouched.
    /// </summary>
    /// <param name="tenantId">The tenant whose roles are counted.</param>
    private async Task<int> CountRolesAsync(Guid tenantId)
        => await DbContext.Roles
            .AcrossAllTenants()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .CountAsync(role => role.TenantId == tenantId, TestContext.Current.CancellationToken);

    /// <summary>
    /// Everything the database said about a refused save, inner exceptions included. The driver reports
    /// the violated constraint by name in its own message, and reading it through the chain keeps the
    /// test project free of a direct dependency on the PostgreSQL driver.
    /// </summary>
    /// <param name="exception">The refusal the save threw.</param>
    private static string ConstraintReportedFor(Exception exception)
    {
        var reported = new List<string>();

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            reported.Add(current.Message);
        }

        return string.Join(" | ", reported);
    }

    /// <summary>
    /// A role name no other test can collide with, within the length the request validators accept.
    /// </summary>
    private static string NewRoleName() => $"reserved-{Guid.NewGuid():N}";

    /// <summary>
    /// What a role creation answered: the status, and the identity it was given when it succeeded.
    /// </summary>
    /// <param name="StatusCode">The status the endpoint answered with.</param>
    /// <param name="Id">The identity of the created role, or the empty identifier when nothing was created.</param>
    private sealed record RoleCreated(HttpStatusCode StatusCode, Guid Id);

    /// <summary>
    /// A row a save refused to persist, kept so the caller can detach it from the change tracker - left
    /// Added it would be retried by this fixture's next save, which would fail a later test for this
    /// one's mistake.
    /// </summary>
    /// <typeparam name="TRow">The kind of row that was refused.</typeparam>
    /// <param name="Row">The refused row, still staged on the change tracker.</param>
    /// <param name="ReportedConstraints">Everything the database said about the refusal.</param>
    private sealed record RefusedInsert<TRow>(TRow Row, string ReportedConstraints);
}