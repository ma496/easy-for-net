namespace Backend.Tests.Features.Tenancy.Core;

using Backend.Exceptions;
using Backend.Features.Identity.Core.Entities;

/// <summary>
/// Tests for the tenant query filter and save-time attribution - the kernel the rest of the tenancy
/// suite stands on (AC-030 to AC-037, AC-080).
/// </summary>
/// <remarks>
/// <para>
/// The entity arranged here is <see cref="Role"/>: tenant-scoped, soft-deletable, and self-contained,
/// so a test can make every row it asserts on without touching another feature's records. Each test
/// creates its own tenants and filters every query by a marker unique to it, so the assertions hold
/// while other tests add rows of the same kind in parallel.
/// </para>
/// <para>
/// The scope is opened on the fixture's own <see cref="Backend.Tenancy.ITenantContext"/>, which is the
/// instance the fixture's <see cref="Backend.Data.AppDbContext"/> reads. Opening one is what stands in
/// for the pre-processor a request would have run; see <c>BackgroundTenantScopeTests</c> for what
/// happens when nobody opens one.
/// </para>
/// </remarks>
public class TenantFilterTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The description written by the bulk-update test, so that the row it did not touch can be shown
    /// to have kept the description it was created with.
    /// </summary>
    private const string TouchedDescription = "Touched by a kernel test";

    /// <summary>
    /// Verifies that a new tenant-scoped row is attributed to the active tenant without the caller
    /// supplying one, which is what keeps a request payload from deciding which tenant a row lands in.
    /// </summary>
    [Fact]
    public async Task Attribution_Is_Applied_On_Save()
    {
        var tenant = await CreateTenantAsync();
        var marker = NewMarker();

        Role role;
        using (TenantContext.BeginTenant(tenant.Id))
        {
            // Deliberately no TenantId: the caller supplies nothing, so the row can only end up
            // attributed if save-time attribution supplied the active tenant for it.
            role = new Role { Name = marker, Description = "Attribution arranged by a kernel test" };
            DbContext.Roles.Add(role);
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        role.TenantId.Should().Be(tenant.Id);

        var stored = await DbContext.Roles
            .AcrossAllTenants()
            .SingleAsync(r => r.NameNormalized == marker, TestContext.Current.CancellationToken);

        stored.TenantId.Should().Be(tenant.Id);
    }

    /// <summary>
    /// Verifies that every shape of a read - list, detail lookup, count and existence check - is
    /// restricted to the active tenant, so that no query shape is left as an accidental way around
    /// the filter.
    /// </summary>
    [Fact]
    public async Task Reads_Are_Restricted_To_The_Active_Tenant()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var marker = NewMarker();
        var roleA = await CreateRoleAsync(tenantA.Id, $"{marker}-a");
        var roleB = await CreateRoleAsync(tenantB.Id, $"{marker}-b");

        using var tenantScope = TenantContext.BeginTenant(tenantA.Id);

        var matches = DbContext.Roles.Where(r => r.NameNormalized.StartsWith(marker));

        var listed = await matches.ToListAsync(TestContext.Current.CancellationToken);
        var found = await matches.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        var counted = await matches.CountAsync(TestContext.Current.CancellationToken);
        var anyOfTenantB = await DbContext.Roles
            .AnyAsync(r => r.Id == roleB.Id, TestContext.Current.CancellationToken);

        listed.Select(r => r.Id).Should().Equal(roleA.Id);
        found.Should().NotBeNull();
        found!.Id.Should().Be(roleA.Id);
        counted.Should().Be(1);
        anyOfTenantB.Should().BeFalse("tenant B's row is not visible from inside tenant A");
    }

    /// <summary>
    /// Verifies that the same restriction applies to statements executed directly by the database
    /// rather than per record, which is the form the notification mark-all-as-read path takes.
    /// </summary>
    [Fact]
    public async Task Bulk_Statements_Are_Restricted()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var marker = NewMarker();
        var roleA = await CreateRoleAsync(tenantA.Id, $"{marker}-a");
        var roleB = await CreateRoleAsync(tenantB.Id, $"{marker}-b");
        var untouchedDescription = roleB.Description;

        int updated;
        int deleted;
        using (TenantContext.BeginTenant(tenantA.Id))
        {
            updated = await DbContext.Roles
                .Where(r => r.NameNormalized.StartsWith(marker))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(r => r.Description, TouchedDescription),
                    TestContext.Current.CancellationToken);

            deleted = await DbContext.Roles
                .Where(r => r.NameNormalized.StartsWith(marker))
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        updated.Should().Be(1, "only tenant A's row is reachable from inside tenant A");
        deleted.Should().Be(1, "and only that one is deleted");

        var remaining = await DbContext.Roles
            .AcrossAllTenants()
            .Where(r => r.NameNormalized.StartsWith(marker))
            .ToListAsync(TestContext.Current.CancellationToken);

        remaining.Select(r => r.Id).Should().Equal(roleB.Id);
        remaining.Single().Description.Should().Be(untouchedDescription);
        roleA.Id.Should().NotBe(roleB.Id);
    }

    /// <summary>
    /// Verifies that tenant restriction and the exclusion of soft-deleted rows hold at the same time
    /// on a kind subject to both, so that applying one never disables the other.
    /// </summary>
    [Fact]
    public async Task Tenant_And_Soft_Delete_Filters_Coexist()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var marker = NewMarker();
        var liveA = await CreateRoleAsync(tenantA.Id, $"{marker}-live-a");
        var deletedA = await CreateRoleAsync(tenantA.Id, $"{marker}-deleted-a", softDeleted: true);
        var liveB = await CreateRoleAsync(tenantB.Id, $"{marker}-live-b");

        using var tenantScope = TenantContext.BeginTenant(tenantA.Id);

        var live = await DbContext.Roles
            .Where(r => r.NameNormalized.StartsWith(marker))
            .ToListAsync(TestContext.Current.CancellationToken);

        live.Select(r => r.Id).Should().Equal(liveA.Id);
        live.Should().NotContain(r => r.Id == deletedA.Id, "the soft-deleted row stays excluded while a tenant is active");

        var acrossTenants = await DbContext.Roles
            .AcrossAllTenants()
            .Where(r => r.NameNormalized.StartsWith(marker))
            .ToListAsync(TestContext.Current.CancellationToken);

        acrossTenants.Select(r => r.Id).Should().BeEquivalentTo([liveA.Id, liveB.Id]);
        acrossTenants.Should().NotContain(
            r => r.Id == deletedA.Id,
            "relaxing tenant restriction by name leaves the soft-delete exclusion in force");
    }

    /// <summary>
    /// Verifies that the sanctioned cross-tenant read relaxes tenant restriction alone, by name, and
    /// needs no tenant scope of its own - the property that lets platform-scoped and maintenance work
    /// span tenants deliberately instead of by a missing tenant.
    /// </summary>
    [Fact]
    public async Task AcrossAllTenants_Is_The_Named_Opt_Out()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var marker = NewMarker();
        var liveA = await CreateRoleAsync(tenantA.Id, $"{marker}-live-a");
        var liveB = await CreateRoleAsync(tenantB.Id, $"{marker}-live-b");
        var deletedA = await CreateRoleAsync(tenantA.Id, $"{marker}-deleted-a", softDeleted: true);

        // No scope is established: the opt-out has to stand on its own rather than being a narrowing
        // of whatever tenant happened to be active.
        using var unscoped = TenantContext.BeginUnscoped();

        var acrossTenants = await DbContext.Roles
            .AcrossAllTenants()
            .Where(r => r.NameNormalized.StartsWith(marker))
            .ToListAsync(TestContext.Current.CancellationToken);

        acrossTenants.Select(r => r.Id).Should().BeEquivalentTo([liveA.Id, liveB.Id]);
        acrossTenants.Should().NotContain(r => r.Id == deletedA.Id);
    }

    /// <summary>
    /// Verifies that a tenant-scoped read reached with no scope established fails rather than
    /// answering from every tenant's rows or from none.
    /// </summary>
    [Fact]
    public async Task Unresolved_Scope_Fails_Rather_Than_Returning_Everything()
    {
        var tenant = await CreateTenantAsync();
        var marker = NewMarker();
        await CreateRoleAsync(tenant.Id, $"{marker}-a");

        using var unscoped = TenantContext.BeginUnscoped();

        // The throw is the whole assertion: a set that answered instead would be a set that answered
        // with rows the caller never established a tenant for.
        var listed = async () => await DbContext.Roles
            .Where(r => r.NameNormalized.StartsWith(marker))
            .ToListAsync(TestContext.Current.CancellationToken);
        var counted = async () => await DbContext.Roles
            .Where(r => r.NameNormalized.StartsWith(marker))
            .CountAsync(TestContext.Current.CancellationToken);

        await listed.Should().ThrowAsync<TenantScopeNotEstablishedException>();
        await counted.Should().ThrowAsync<TenantScopeNotEstablishedException>();
    }

    /// <summary>
    /// Verifies that a tenant-scoped row persisted with no attribution is rejected rather than stored,
    /// so that no query can ever reach a row belonging to no tenant.
    /// </summary>
    [Fact]
    public async Task Unattributed_Write_Is_Rejected()
    {
        var marker = NewMarker();

        Role role;
        using (TenantContext.BeginUnscoped())
        {
            role = new Role { Name = marker, Description = "Rejected by a kernel test" };
            DbContext.Roles.Add(role);

            var saved = async () => await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await saved.Should().ThrowAsync<TenantScopeNotEstablishedException>();
        }

        // The rejected entry is dropped from the change tracker: left Added it would be retried by the
        // next save on this fixture's context, which would fail a later test for this one's mistake.
        DbContext.Entry(role).State = EntityState.Detached;

        var stored = await DbContext.Roles
            .AcrossAllTenants()
            .AnyAsync(r => r.NameNormalized == marker, TestContext.Current.CancellationToken);

        stored.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that a row the caller has attributed to a tenant other than the active one is refused
    /// rather than stored, so that supplying an attribution is never a way to write into a tenant the
    /// caller is not acting in (AC-030).
    /// </summary>
    [Fact]
    public async Task Attribution_Supplied_By_The_Caller_Is_Refused()
    {
        var activeTenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();
        var marker = NewMarker();

        Role role;
        using (TenantContext.BeginTenant(activeTenant.Id))
        {
            // The caller names a tenant of its own, which is exactly what the stamp has to ignore: the
            // active tenant decides where a row lands, not the payload.
            role = new Role { Name = marker, Description = "Attributed by a kernel test", TenantId = otherTenant.Id };
            DbContext.Roles.Add(role);

            var saved = async () => await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await saved.Should().ThrowAsync<TenantAttributionException>();
        }

        DbContext.Entry(role).State = EntityState.Detached;

        var stored = await DbContext.Roles
            .AcrossAllTenants()
            .AnyAsync(r => r.NameNormalized == marker, TestContext.Current.CancellationToken);

        stored.Should().BeFalse("the refused row was not quietly written to the tenant the caller named instead");
    }

    /// <summary>
    /// Verifies that an existing row is never moved from the tenant it was created in, so that a row's
    /// attribution is decided once and a change to it is refused rather than applied (AC-030).
    /// </summary>
    [Fact]
    public async Task Reattributing_An_Existing_Row_Is_Refused()
    {
        var createdIn = await CreateTenantAsync();
        var movedTo = await CreateTenantAsync();
        var marker = NewMarker();
        var role = await CreateRoleAsync(createdIn.Id, marker);

        using (TenantContext.BeginTenant(createdIn.Id))
        {
            role.TenantId = movedTo.Id;

            var saved = async () => await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await saved.Should().ThrowAsync<TenantAttributionException>();
        }

        DbContext.Entry(role).State = EntityState.Detached;

        var stored = await DbContext.Roles
            .AcrossAllTenants()
            .SingleAsync(r => r.NameNormalized == marker, TestContext.Current.CancellationToken);

        stored.TenantId.Should().Be(createdIn.Id, "the row still belongs to the tenant it was created in");
    }

    /// <summary>
    /// A marker no other test can match, in a shape valid for both a role name and the normalized
    /// column it is compared through.
    /// </summary>
    private static string NewMarker() => $"kernel-{Guid.NewGuid():N}";

    /// <summary>
    /// Arranges one role inside a tenant, optionally soft-deleted, and leaves it tracked so a caller
    /// can read back what the save did to it.
    /// </summary>
    /// <param name="tenantId">The tenant the role belongs to.</param>
    /// <param name="name">The role's name, unique to the test that made it.</param>
    /// <param name="softDeleted">Whether to soft-delete the row after creating it.</param>
    /// <returns>The arranged role.</returns>
    private async Task<Role> CreateRoleAsync(Guid tenantId, string name, bool softDeleted = false)
    {
        using var tenantScope = TenantContext.BeginTenant(tenantId);

        var role = new Role { Name = name, Description = "Role arranged by a kernel test" };
        DbContext.Roles.Add(role);
        await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (softDeleted)
        {
            role.IsDeleted = true;
            role.DeletedAt = DateTime.UtcNow;
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return role;
    }
}
