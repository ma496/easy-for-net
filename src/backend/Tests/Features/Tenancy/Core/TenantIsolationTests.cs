using Backend.Features.Tenancy.Core.Entities;

namespace Backend.Tests.Features.Tenancy.Core;

using Backend.Data.Entities;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests for what one tenant's records look like from inside another - the same, whether they belong
/// to somebody else or to nobody at all (AC-032, AC-087, AC-111).
/// </summary>
/// <remarks>
/// <para>
/// Every test here arranges two tenants of its own and drives the role endpoints, because a role is
/// the one tenant-scoped kind a tenant's own administrator can read, list, rename and delete without
/// reaching outside the identity slice. The caller is a purpose-built administrator of one tenant - a
/// role holding exactly <c>Role_View</c>, <c>Role_Update</c>, <c>Role_Delete</c> and <c>Role_Create</c>
/// and nothing else - so what the refusals prove is tenant isolation rather than a permission gate, and
/// platform administration is deliberately absent: a platform administrator reads every tenant's roles,
/// so a test that used one could not tell isolation from authority.
/// </para>
/// <para>
/// The refusals are compared against the answer the very same endpoint gives for a random
/// <see cref="Guid"/>: an identical status and an identical body, read raw rather than deserialized, so
/// a response that leaked even a hint - a different message, a field name, a length - would differ from
/// the missing-record answer and fail the comparison.
/// </para>
/// </remarks>
public class TenantIsolationTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that reading a record belonging to another tenant is answered exactly as reading one
    /// that does not exist, so nothing about the record - not its name, not its permissions, not its
    /// existence - can be learned by asking for it (AC-032).
    /// </summary>
    [Fact]
    public async Task Cross_Tenant_Read_Responds_As_Missing()
    {
        var arrangement = await ArrangeTwoTenantsAsync();

        var otherTenantsRole = await ReadAsync(arrangement.AdministratorOfB, arrangement.RoleInA.Id);
        var absentRole = await ReadAsync(arrangement.AdministratorOfB, Guid.NewGuid());

        otherTenantsRole.Status.Should().Be(HttpStatusCode.NotFound);
        otherTenantsRole.Should().Be(absentRole, "another tenant's role is answered exactly as a role that never existed");
    }

    /// <summary>
    /// Verifies that renaming a record belonging to another tenant is refused as a missing record and
    /// leaves the record exactly as it stood (AC-087).
    /// </summary>
    [Fact]
    public async Task Cross_Tenant_Update_Is_Refused()
    {
        var arrangement = await ArrangeTwoTenantsAsync();

        var otherTenantsRole = await UpdateAsync(arrangement.AdministratorOfB, arrangement.RoleInA.Id);
        var absentRole = await UpdateAsync(arrangement.AdministratorOfB, Guid.NewGuid());

        otherTenantsRole.Status.Should().Be(HttpStatusCode.NotFound);
        otherTenantsRole.Should().Be(absentRole);

        var stored = await ReloadAsync(arrangement.RoleInA.Id);
        stored!.Name.Should().Be(arrangement.RoleInA.Name, "the rename reached nothing");
    }

    /// <summary>
    /// Verifies that deleting a record belonging to another tenant is refused as a missing record and
    /// leaves the record in place, still resolvable by its own tenant (AC-087).
    /// </summary>
    [Fact]
    public async Task Cross_Tenant_Delete_Is_Refused()
    {
        var arrangement = await ArrangeTwoTenantsAsync();

        var otherTenantsRole = await DeleteAsync(arrangement.AdministratorOfB, arrangement.RoleInA.Id);
        var absentRole = await DeleteAsync(arrangement.AdministratorOfB, Guid.NewGuid());

        otherTenantsRole.Status.Should().Be(HttpStatusCode.NotFound);
        otherTenantsRole.Should().Be(absentRole);

        var stored = await ReloadAsync(arrangement.RoleInA.Id);
        stored.Should().NotBeNull("the delete reached nothing");
        stored!.IsDeleted.Should().BeFalse();

        // The owner still sees it, so the record was refused to the other tenant rather than deleted
        // for everybody - the difference between isolation and a lost record.
        var ownerReadsIt = await ReadAsync(arrangement.AdministratorOfA, arrangement.RoleInA.Id);
        ownerReadsIt.Status.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies that a record belonging to another tenant is absent from a list and a search as well as
    /// from a lookup, so no shape of read is left as a way around the restriction (AC-087).
    /// </summary>
    [Fact]
    public async Task Cross_Tenant_Record_Is_Not_Listed()
    {
        var arrangement = await ArrangeTwoTenantsAsync();

        var searchForItsName = new RoleListRequest { Search = arrangement.RoleInA.Name, All = true };

        var (_, seenByOwner) = await arrangement.AdministratorOfA
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(searchForItsName);
        var (_, seenByOtherTenant) = await arrangement.AdministratorOfB
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(searchForItsName);

        seenByOwner.Items.Select(role => role.Id).Should().Equal(arrangement.RoleInA.Id);
        seenByOwner.Total.Should().Be(1);

        seenByOtherTenant.Items.Should().BeEmpty("another tenant's role is not listed here");
        seenByOtherTenant.Total.Should().Be(0, "nor is it counted towards this tenant's total");
    }

    /// <summary>
    /// Verifies that every verb which addresses a role - read, update and delete - answers a role of
    /// another tenant as a missing one, and that the role's name and its permission set are exactly what
    /// they were afterwards, so the attempts neither disclosed nor changed anything (AC-111).
    /// </summary>
    [Fact]
    public async Task Cross_Tenant_Role_Responds_As_Missing()
    {
        var arrangement = await ArrangeTwoTenantsAsync();
        var absentRoleId = Guid.NewGuid();

        var read = await ReadAsync(arrangement.AdministratorOfB, arrangement.RoleInA.Id);
        var update = await UpdateAsync(arrangement.AdministratorOfB, arrangement.RoleInA.Id);
        var delete = await DeleteAsync(arrangement.AdministratorOfB, arrangement.RoleInA.Id);

        var readAbsent = await ReadAsync(arrangement.AdministratorOfB, absentRoleId);
        var updateAbsent = await UpdateAsync(arrangement.AdministratorOfB, absentRoleId);
        var deleteAbsent = await DeleteAsync(arrangement.AdministratorOfB, absentRoleId);

        read.Should().Be(readAbsent);
        update.Should().Be(updateAbsent);
        delete.Should().Be(deleteAbsent);

        var stored = await ReloadAsync(arrangement.RoleInA.Id);
        stored.Should().NotBeNull();
        stored!.Name.Should().Be(arrangement.RoleInA.Name);
        stored.NameNormalized.Should().Be(arrangement.RoleInA.NameNormalized);

        var permissions = await DbContext.RolePermissions
            .Where(rolePermission => rolePermission.RoleId == arrangement.RoleInA.Id)
            .Select(rolePermission => rolePermission.PermissionId)
            .ToListAsync(TestContext.Current.CancellationToken);

        permissions.Should().Equal(arrangement.RoleInA.PermissionIds, "the role's authority is untouched");
    }

    /// <summary>
    /// Arranges two tenants of its own, one role inside the first of them carrying exactly one
    /// permission, and an administrator client for each tenant. Both administrators are purpose-built
    /// rather than seeded, so neither carries platform administration and the answers below are decided
    /// by tenant standing alone.
    /// </summary>
    private async Task<Arrangement> ArrangeTwoTenantsAsync()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();

        var roleInA = await CreateRoleAsync(tenantA.Id, Allow.Role_View);

        return new Arrangement(tenantA,
                               tenantB,
                               roleInA,
                               await CreateTenantAdministratorAsync(tenantA.Id),
                               await CreateTenantAdministratorAsync(tenantB.Id));
    }

    /// <summary>
    /// Creates an account inside a tenant holding a role that carries exactly the four role
    /// permissions, and returns a client presenting that account's tenant-scoped session.
    /// </summary>
    /// <param name="tenantId">The tenant the administrator is to act in.</param>
    private async Task<HttpClient> CreateTenantAdministratorAsync(Guid tenantId)
    {
        var roleId = await CreateTenantRoleAsync(tenantId,
                                                 Allow.Role_View,
                                                 Allow.Role_Create,
                                                 Allow.Role_Update,
                                                 Allow.Role_Delete);
        var administrator = await CreateTenantUserAsync(tenantId, roleId);
        return await ClientForAsync(administrator.Username, tenantId);
    }

    /// <summary>
    /// Arranges one role inside a tenant, carrying exactly the permissions named and a name no other
    /// role or test can collide with.
    /// </summary>
    /// <param name="tenantId">The tenant the role belongs to.</param>
    /// <param name="permissions">The permission names the role is to hold.</param>
    private async Task<ArrangedRole> CreateRoleAsync(Guid tenantId, params string[] permissions)
    {
        using var tenantScope = TenantContext.BeginTenant(tenantId);

        var role = new Role
        {
            SystemCreated = false,
            Name = $"isolated-{Guid.NewGuid():N}",
            Description = "Role arranged by an isolation test"
        };
        DbContext.Roles.Add(role);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var permissionIds = await DbContext.Permissions
            .Where(permission => permissions.Contains(permission.Name))
            .Select(permission => permission.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        DbContext.RolePermissions.AddRange(permissionIds.Select(permissionId => new RolePermission
        {
            RoleId = role.Id,
            PermissionId = permissionId
        }));
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new ArrangedRole(role.Id, role.Name, role.NameNormalized, permissionIds);
    }

    /// <summary>
    /// Reads a role as the named caller, returning the status and the raw body rather than a
    /// deserialized response - the body is what makes two refusals comparable.
    /// </summary>
    /// <param name="client">The caller making the request.</param>
    /// <param name="id">The role being read.</param>
    private async Task<Answer> ReadAsync(HttpClient client, Guid id)
    {
        var (response, _) = await client
            .GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(new() { Id = id });

        return await Answer.OfAsync(response);
    }

    /// <summary>
    /// Renames a role as the named caller, with a name that would be valid were the role reachable.
    /// </summary>
    /// <param name="client">The caller making the request.</param>
    /// <param name="id">The role being renamed.</param>
    private async Task<Answer> UpdateAsync(HttpClient client, Guid id)
    {
        var (response, _) = await client.PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, RoleUpdateResponse>(
            new()
            {
                Id = id,
                Name = $"renamed-{Guid.NewGuid():N}",
                Description = "Renamed by an isolation test"
            });

        return await Answer.OfAsync(response);
    }

    /// <summary>
    /// Deletes a role as the named caller.
    /// </summary>
    /// <param name="client">The caller making the request.</param>
    /// <param name="id">The role being deleted.</param>
    private async Task<Answer> DeleteAsync(HttpClient client, Guid id)
    {
        var (response, _) = await client
            .DELETEAsync<RoleDeleteEndpoint, RoleDeleteRequest, RoleDeleteResponse>(new() { Id = id });

        return await Answer.OfAsync(response);
    }

    /// <summary>
    /// Reads a role back from the database rather than from the change tracker, so what is asserted is
    /// what was persisted.
    /// </summary>
    /// <param name="id">The role being read.</param>
    private async Task<Role?> ReloadAsync(Guid id)
        => await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .FirstOrDefaultAsync(role => role.Id == id, TestContext.Current.CancellationToken);

    /// <summary>
    /// The status and raw body of one refusal, which is what makes "answered exactly as a missing
    /// record" a comparison rather than a claim.
    /// </summary>
    /// <param name="Status">The status code the caller received.</param>
    /// <param name="Body">The response body exactly as it was sent.</param>
    private sealed record Answer(HttpStatusCode Status, string Body)
    {
        /// <summary>
        /// Reads the status and body off a response. The body is read here rather than by the typed
        /// call, so two answers are compared on what the server actually sent.
        /// </summary>
        /// <param name="response">The response to read.</param>
        public static async Task<Answer> OfAsync(HttpResponseMessage response)
            => new(response.StatusCode,
                   await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The two tenants a test arranged together with the role it placed in the first of them.
    /// </summary>
    /// <param name="TenantA">The tenant the role belongs to.</param>
    /// <param name="TenantB">The tenant the requests below are made from.</param>
    /// <param name="RoleInA">The role placed in <paramref name="TenantA"/>.</param>
    /// <param name="AdministratorOfA">A client acting as an administrator of <paramref name="TenantA"/>.</param>
    /// <param name="AdministratorOfB">A client acting as an administrator of <paramref name="TenantB"/>.</param>
    private sealed record Arrangement(Tenant TenantA,
                                      Tenant TenantB,
                                      ArrangedRole RoleInA,
                                      HttpClient AdministratorOfA,
                                      HttpClient AdministratorOfB);

    /// <summary>
    /// A role as it was written, kept beside the arrangement so the assertions compare against what the
    /// test created rather than against a second read that could agree with a wrong answer.
    /// </summary>
    /// <param name="Id">The role's identity.</param>
    /// <param name="Name">The name as it was stored.</param>
    /// <param name="NameNormalized">The normalized name the uniqueness comparison runs against.</param>
    /// <param name="PermissionIds">The permissions the role holds.</param>
    private sealed record ArrangedRole(Guid Id, string Name, string NameNormalized, List<Guid> PermissionIds);
}