namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests for <see cref="TenantCreateEndpoint"/>: creating a tenant from the platform, the naming rules
/// the request is held to, the audit it is recorded with and the administrator role the new tenant is
/// provisioned with (AC-002 - AC-004, AC-012, AC-042, AC-078, AC-100 - AC-102).
/// </summary>
/// <remarks>
/// <para>
/// Every tenant asserted on here is created by the test through the endpoint under test, so nothing
/// depends on a tenant the seeder made or on another test's rows: the suite shares one database and
/// runs its collections in parallel.
/// </para>
/// <para>
/// The identifier each case submits carries a fresh <see cref="Guid"/>, because the uniqueness rule is
/// enforced against retained rows as well as live ones, so it is a value that has to be new rather than
/// merely a value that is valid. The one exception is the case set that is about the identifier's own
/// rules, whose values are refused before anything is written.
/// </para>
/// </remarks>
public class TenantCreateTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a platform administrator creating a tenant with a valid name and identifier is
    /// given its assigned identity, and that the tenant is persisted active and marked as a caller's
    /// rather than as the platform's own (AC-002).
    /// </summary>
    [Fact]
    public async Task Valid_Input()
    {
        await SetPlatformAdminAuthTokenAsync();

        var identifier = NewTenantIdentifier();
        var (response, created) = await App.Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, TenantCreateResponse>(
                new() { Name = "Acme Incorporated", Identifier = identifier });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        created.Id.Should().NotBe(Guid.Empty, "a created tenant is answered with the identity it was given");

        var stored = await ReloadTenantAsync(created.Id);

        stored.Status.Should().Be(TenantStatus.Active, "a tenant is born active, and no payload can claim otherwise");
        stored.SystemCreated.Should().BeFalse("only the seeder's bootstrap tenant is the platform's own");
        stored.Name.Should().Be("Acme Incorporated");
        stored.Identifier.Should().Be(identifier);
        stored.IdentifierNormalized.Should().Be(identifier, "the identifier is already in the form the comparison uses");
    }

    /// <summary>
    /// Verifies that creating a tenant under an identifier another tenant already holds is refused with
    /// the code belonging to that field, and that the refusal persists nothing (AC-003).
    /// </summary>
    [Fact]
    public async Task Duplicate_Identifier_Is_Refused()
    {
        var incumbent = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await App.Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, ProblemDetails>(
                new() { Name = "A Second Acme", Identifier = incumbent.Identifier });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle("the identifier is the one thing wrong with the request");
        problem.Errors.First().Name.Should().Be("identifier", "the caller is told which value was refused");
        problem.Errors.First().Code.Should().Be(ErrorCodes.TenantIdentifierAlreadyExists);

        var holders = await DbContext.Tenants
            .AcrossAllTenants()
            .AsNoTracking()
            .CountAsync(
                tenant => tenant.IdentifierNormalized == incumbent.IdentifierNormalized,
                TestContext.Current.CancellationToken);

        holders.Should().Be(1, "the refused request persisted no tenant, so the incumbent holds the identifier alone");
    }

    /// <summary>
    /// Verifies that an identifier differing from a taken one only in case is never accepted, which is
    /// what the comparison having no regard to case means from the outside (AC-003).
    /// </summary>
    /// <remarks>
    /// The answer is a 400 naming the identifier rather than the duplicate code, because the identifier
    /// shape admits lower-case values only and an upper-cased one is turned away at the door. That is
    /// still the refusal AC-003 asks for - the value is not taken - and it is the only form of it
    /// reachable through this endpoint, since the shape rule refuses the mixed-case form of every
    /// identifier a caller could send. The normalized comparison itself is proved directly, with a
    /// mixed-case identifier written by the creation path, in
    /// <c>TenantUniquenessTests.Identifier_Is_Stored_Trimmed_With_A_Normalized_Copy</c>.
    /// </remarks>
    [Fact]
    public async Task Identifier_Differing_Only_In_Case_Is_Refused()
    {
        var incumbent = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await App.Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, ProblemDetails>(
                new() { Name = "A Second Acme", Identifier = incumbent.Identifier.ToUpperInvariant() });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().Contain(error => error.Name == "identifier");

        var holders = await DbContext.Tenants
            .AcrossAllTenants()
            .AsNoTracking()
            .CountAsync(
                tenant => tenant.IdentifierNormalized == incumbent.IdentifierNormalized,
                TestContext.Current.CancellationToken);

        holders.Should().Be(1, "nothing was persisted, so the identifier is still the incumbent's alone");
    }

    /// <summary>
    /// Verifies that a request breaking more than one rule is answered with a failure against each field
    /// that broke one, rather than with the first failure found (AC-004).
    /// </summary>
    [Fact]
    public async Task Invalid_Input()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await App.Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, ProblemDetails>(
                new() { Name = string.Empty, Identifier = "ab" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Select(error => error.Name).Should().Equal(
            ["name", "identifier"],
            "both fields broke a rule, and each failure names the field it belongs to");
    }

    /// <summary>
    /// Verifies the bounds of the display name: a name shorter than the minimum or longer than the
    /// maximum is refused against the name field, and a name padded with white-space is accepted and
    /// stored trimmed (AC-100).
    /// </summary>
    /// <param name="name">The display name to submit.</param>
    /// <param name="accepted">Whether the name is within the declared bounds once trimmed.</param>
    /// <remarks>
    /// The two outcomes are read as the payload each one actually is - the failure as a
    /// <see cref="ProblemDetails"/> and the success as a <see cref="TenantCreateResponse"/> - because
    /// the two bodies carry the same field names with different types, so one type cannot describe both.
    /// </remarks>
    [Theory]
    [MemberData(nameof(NameCases))]
    public async Task Name_Length_Rules(string name, bool accepted)
    {
        await SetPlatformAdminAuthTokenAsync();

        var identifier = NewTenantIdentifier();
        var request = new TenantCreateRequest { Name = name, Identifier = identifier };

        if (!accepted)
        {
            var (refused, problem) = await App.Client
                .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, ProblemDetails>(request);

            refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            problem.Errors.Should().Contain(
                error => error.Name == "name",
                "the display name is what broke its bounds, so the failure names that field");

            (await TenantService.IdentifierExistsAsync(identifier, cancellationToken: TestContext.Current.CancellationToken))
                .Should().BeFalse("a refused request persists nothing");
            return;
        }

        var (response, _) = await App.Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, TenantCreateResponse>(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var stored = await DbContext.Tenants
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(tenant => tenant.IdentifierNormalized == identifier, TestContext.Current.CancellationToken);

        stored.Name.Should().Be(
            name.Trim(),
            "the name is measured and stored trimmed, so a value that is long enough only once padded is accepted as itself");
    }

    /// <summary>
    /// Verifies the shape and the bounds of the identifier: a value outside them is refused against the
    /// identifier field, and a value inside them is accepted (AC-101).
    /// </summary>
    /// <param name="identifier">The identifier to submit.</param>
    /// <param name="accepted">Whether the value is a legal identifier.</param>
    /// <remarks>
    /// The accepted branch is asserted against what the request left behind rather than against the body
    /// it answered with: the identifier being held is the fact a success has to make true.
    /// </remarks>
    [Theory]
    [MemberData(nameof(IdentifierCases))]
    public async Task Identifier_Format_Rules(string identifier, bool accepted)
    {
        await SetPlatformAdminAuthTokenAsync();

        var request = new TenantCreateRequest { Name = "Acme Incorporated", Identifier = identifier };

        if (!accepted)
        {
            var (refused, problem) = await App.Client
                .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, ProblemDetails>(request);

            refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            problem.Errors.Should().Contain(
                error => error.Name == "identifier",
                "the identifier is what broke its bounds or its shape, so the failure names that field");

            (await TenantService.IdentifierExistsAsync(identifier, cancellationToken: TestContext.Current.CancellationToken))
                .Should().BeFalse("a refused request persists nothing");
            return;
        }

        var (response, _) = await App.Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, TenantCreateResponse>(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TenantService.IdentifierExistsAsync(identifier, cancellationToken: TestContext.Current.CancellationToken))
            .Should().BeTrue("an accepted request is persisted as asked");
    }

    /// <summary>
    /// Verifies that a tenant records who created it and when, and who last changed it and when, so the
    /// platform can answer who made a tenant and who renamed it (AC-012, AC-078).
    /// </summary>
    [Fact]
    public async Task Records_Audit_Fields()
    {
        await SetPlatformAdminAuthTokenAsync();

        var before = DateTime.UtcNow;
        var identifier = NewTenantIdentifier();

        var (createResponse, created) = await App.Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, TenantCreateResponse>(
                new() { Name = "Acme Incorporated", Identifier = identifier });

        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterCreate = DateTime.UtcNow;
        var stored = await ReloadTenantAsync(created.Id);

        stored.CreatedBy.Should().Be(TestUsers.PlatformAdminUserId, "the creating account is the caller that asked for the tenant");
        stored.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(afterCreate, "the creation time is the time of the request");
        stored.UpdatedBy.Should().BeNull("nothing has changed the tenant since it was made");
        stored.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(
            afterCreate,
            "a row carries its creation as its last change until something changes it");

        var renamed = $"{NewTenantIdentifier()}-renamed";
        var (updateResponse, _) = await App.Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, TenantUpdateResponse>(
                new() { Id = created.Id, Name = "Acme Holdings", Identifier = renamed });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterUpdate = DateTime.UtcNow;
        var updated = await ReloadTenantAsync(created.Id);

        updated.Name.Should().Be("Acme Holdings");
        updated.UpdatedBy.Should().Be(TestUsers.PlatformAdminUserId, "the updating account is the caller that asked for the rename");
        updated.UpdatedAt.Should().NotBeNull();
        updated.UpdatedAt!.Value.Should().BeOnOrAfter(stored.CreatedAt).And.BeOnOrBefore(afterUpdate);
    }

    /// <summary>
    /// Verifies that a new tenant is provisioned with a system-created administrator role holding every
    /// tenant-level permission and no platform-tier one, so it is administrable from inside itself the
    /// moment it has a member (AC-042).
    /// </summary>
    /// <remarks>
    /// The role is provisioned before any member exists, and a tenant created from the platform has no
    /// member: assigning it to a first member is what onboarding and the member surfaces do, and is
    /// covered where each of those lives. What is proved here is the role that waits for that member.
    /// </remarks>
    [Fact]
    public async Task Provisions_A_System_Created_Administrator_Role()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, created) = await App.Client
            .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, TenantCreateResponse>(
                new() { Name = "Acme Incorporated", Identifier = NewTenantIdentifier() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var roles = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.TenantId == created.Id)
            .Select(role => new { role.Id, role.SystemCreated })
            .ToListAsync(TestContext.Current.CancellationToken);

        var administrator = roles
            .Should().ContainSingle("a tenant is provisioned with one role - the administrator it is governed through")
            .Subject;

        administrator.SystemCreated.Should().BeTrue("the tenant's administrator is declared by code, not by a caller");

        var held = await ReadPermissionsHeldInRoleAsync(administrator.Id);
        var tenantPermissionNames = App.Services
            .GetRequiredService<IPermissionDefinitionService>()
            .GetPermissionNamesInScope(PermissionScope.Tenant);

        held.Should().BeEquivalentTo(
            tenantPermissionNames,
            "the tenant's administrator holds every permission exercisable inside a tenant and no platform-scoped one -"
            + " those are held through a role of the platform's own, so a tenant's administrator cannot grant them");

        var members = await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .CountAsync(membership => membership.TenantId == created.Id, TestContext.Current.CancellationToken);

        members.Should().Be(0, "a tenant created from the platform has no first member until one is added to it");
    }

    /// <summary>
    /// The display names the bounds are proved with: one character short of the minimum, one past the
    /// maximum, the maximum itself, and a value that reaches the minimum only once it is trimmed.
    /// </summary>
    /// <remarks>
    /// The lengths are taken from <see cref="TenantValidationRules"/> rather than written out, so a case
    /// set that is about the declared bounds cannot drift away from them.
    /// </remarks>
    public static TheoryData<string, bool> NameCases => new()
    {
        { string.Empty, false },
        { new string('a', TenantValidationRules.NameMinLength - 1), false },
        { new string('a', TenantValidationRules.NameMaxLength + 1), false },
        { new string('a', TenantValidationRules.NameMaxLength), true },
        { "  ab  ", true }
    };

    /// <summary>
    /// The identifiers the shape and the bounds are proved with: values that are too short and too long,
    /// a leading, trailing and doubled hyphen, a character outside the permitted set, an upper-cased
    /// value, and two that are legal.
    /// </summary>
    /// <remarks>
    /// Only the accepted values have to be unique - a refused identifier is persisted nowhere - and both
    /// of them are built rather than written out, so a case set that is about the shape cannot be broken
    /// by a name some earlier run of the suite left behind.
    /// </remarks>
    public static TheoryData<string, bool> IdentifierCases => new()
    {
        { new string('a', TenantValidationRules.IdentifierMinLength - 1), false },
        { new string('a', TenantValidationRules.IdentifierMaxLength + 1), false },
        { "-acme", false },
        { "acme-", false },
        { "ac--me", false },
        { "ac_me", false },
        { "ac me", false },
        { "Acme", false },
        { $"acme-{Guid.NewGuid():N}", true },
        { NewLongestIdentifier(), true }
    };

    /// <summary>
    /// An identifier of exactly the maximum permitted length, and like every accepted value in the case
    /// set one no other run can hold already.
    /// </summary>
    /// <returns>The identifier.</returns>
    private static string NewLongestIdentifier()
    {
        var unique = Guid.NewGuid().ToString("N");
        return new string('a', TenantValidationRules.IdentifierMaxLength - unique.Length) + unique;
    }

    /// <summary>
    /// Reads a tenant back from the database rather than from the change tracker, so an assertion is
    /// about what was persisted and not about the instance the request happened to leave behind.
    /// </summary>
    /// <param name="tenantId">The tenant being read.</param>
    /// <returns>The stored tenant.</returns>
    private async Task<Tenant> ReloadTenantAsync(Guid tenantId)
        => await DbContext.Tenants
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(tenant => tenant.Id == tenantId, TestContext.Current.CancellationToken);

    /// <summary>
    /// The permission names a role holds, read through the junction table the grants live in.
    /// </summary>
    /// <param name="roleId">The role being read.</param>
    /// <returns>The names of the permissions the role grants.</returns>
    private async Task<List<string>> ReadPermissionsHeldInRoleAsync(Guid roleId)
        => await DbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => rolePermission.RoleId == roleId)
            .Select(rolePermission => rolePermission.Permission.Name)
            .ToListAsync(TestContext.Current.CancellationToken);
}
