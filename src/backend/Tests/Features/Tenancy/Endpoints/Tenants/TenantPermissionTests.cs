namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using Backend.Data.Entities;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Microsoft.AspNetCore.Routing;
using System.Net.Http.Json;

/// <summary>
/// Tests that every operation on the tenancy surface is gated by a named permission, that a caller who
/// does not hold it is turned away before the operation runs, and that the endpoint's own declaration -
/// rather than the caller or the request it sent - is what decides it
/// (AC-044, AC-045, AC-086, AC-088).
/// </summary>
/// <remarks>
/// <para>
/// The three methods answer three different questions. <see cref="Every_Tenant_Endpoint_Declares_Its_Permission"/>
/// reads the live routing table, so what it asserts is the map the framework enforces rather than a
/// copy of the source it was written from. <see cref="Missing_Permission_Is_Forbidden"/> sends every
/// gated endpoint its own valid request as a caller holding only <see cref="Allow.Tenant_View"/>, and
/// asserts both halves of the answer: a 403 where the caller's authority falls short, and - through
/// the permission-holding caller sending the very same request - that the request was otherwise one
/// that would have been carried out. A refused operation must also have left no trace, which is what
/// separates a gate that turned the request away from a handler that ran and refused it.
/// </para>
/// <para>
/// The caller is <c>limited</c>, a member of the bootstrap tenant holding
/// <see cref="TestRoles.LimitedTenantRoleId"/> and nothing else. Every other seeded role holds every
/// permission, so a refusal proved with one of those would be unprovable: it would read the same
/// whether or not the endpoint declared anything at all. That is the whole of AC-088, and
/// <see cref="Purpose_Built_Role_Proves_The_Gate"/> goes a step further by moving a caller's authority
/// one permission at a time inside a tenant the test made, showing the reachable set of endpoints
/// follows the declarations exactly.
/// </para>
/// <para>
/// The endpoints on this surface are all marked <see cref="AllowNoTenantAttribute"/>, which is what
/// makes the refusals below permission refusals rather than tenant ones: an endpoint exempt from the
/// tenant requirement runs for a caller whose tenant is unusable, so whatever it turns away, it turns
/// away for want of authority. Each refusal is therefore asserted on both halves of AC-045 - the
/// standard forbidden status, and the error code that says the caller's roles are what fell short -
/// which is the same code a caller acting in a healthy tenant without the permission is refused with.
/// </para>
/// </remarks>
public class TenantPermissionTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The soft-delete query filter's registered key, named so a read that has to see a retained row
    /// can relax that one filter and leave every other in force.
    /// </summary>
    private const string SoftDeleteFilterKey = "SoftDelete";

    /// <summary>
    /// The namespace the tenancy surface's endpoints live in, matched as a prefix so an endpoint added
    /// to a new area of the same feature is covered by the reflection below rather than escaping it.
    /// </summary>
    private const string TenancyEndpointNamespace = "Backend.Features.Tenancy.Endpoints";

    /// <summary>
    /// The seeded account holding <see cref="TestRoles.LimitedTenantRoleId"/> and no other role
    /// anywhere, and therefore exactly one permission.
    /// </summary>
    private const string LimitedUsername = "limited";

    /// <summary>
    /// The permission every endpoint of the tenancy surface declares, or <see langword="null"/> for the
    /// two that declare none. One entry per endpoint, and one endpoint per entry: the reflection test
    /// fails as loudly on an endpoint missing from this map as on a permission whose value changed.
    /// </summary>
    private static readonly Dictionary<Type, string?> DeclaredPermissions = new()
    {
        // The platform tier: creating, renaming, reading, suspending, reactivating and deleting a
        // tenant are acts on the tenant itself, so no tenant role can carry them.
        [typeof(TenantCreateEndpoint)] = Allow.Tenant_Create,
        [typeof(TenantUpdateEndpoint)] = Allow.Tenant_Update,
        [typeof(TenantDeleteEndpoint)] = Allow.Tenant_Delete,
        [typeof(TenantSuspendEndpoint)] = Allow.Tenant_Suspend,
        [typeof(TenantReactivateEndpoint)] = Allow.Tenant_Reactivate,

        // Reading is the one thing a caller inside a tenant is granted without administering it.
        [typeof(TenantGetEndpoint)] = Allow.Tenant_View,
        [typeof(TenantListEndpoint)] = Allow.Tenant_View,

        // The membership tier: administering who belongs to a tenant, which a tenant's own
        // administrator holds and the platform administrator holds over every tenant.
        [typeof(TenantMemberListEndpoint)] = Allow.TenantMember_View,
        [typeof(TenantMemberAddEndpoint)] = Allow.TenantMember_Add,
        [typeof(TenantMemberRemoveEndpoint)] = Allow.TenantMember_Remove,
        [typeof(TenantMemberUpdateRolesEndpoint)] = Allow.TenantMember_UpdateRoles,

        // No permission at all, and deliberately so: these two are how a caller acquires a tenant to
        // act in, so gating them behind a permission held inside a tenant would be circular. They are
        // gated by authentication instead, which is the whole of what they require.
        [typeof(TenantOnboardEndpoint)] = null,
        [typeof(TenantSwitchEndpoint)] = null
    };

    /// <summary>
    /// The name of every endpoint the map above gives a permission, as the theory below is driven by.
    /// </summary>
    public static TheoryData<string> GatedEndpointNames
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var declared in DeclaredPermissions.Where(entry => entry.Value is not null))
            {
                names.Add(declared.Key.Name);
            }

            return names;
        }
    }

    /// <summary>
    /// Verifies that every endpoint of the tenancy surface is routed and declares exactly the
    /// permission this document says it does, and that the two endpoints that establish a caller's
    /// tenant declare none - so an endpoint added later, or a constant whose value changed, fails here
    /// rather than silently opening an operation to whoever can reach it (AC-044).
    /// </summary>
    /// <remarks>
    /// The declarations are read off the live endpoint definitions - the same objects the authorization
    /// middleware reads when it decides a request - so this is not a second copy of the source's own
    /// text and cannot drift from what is enforced. The reflection side is what makes the map complete:
    /// the set of endpoint types it finds must be exactly the set the map accounts for, so a new
    /// endpoint cannot be added to the surface without being given a permission here.
    /// </remarks>
    [Fact]
    public void Every_Tenant_Endpoint_Declares_Its_Permission()
    {
        var routed = LiveEndpointDefinitions();

        var tenancyEndpoints = typeof(TenantCreateEndpoint).Assembly
            .GetTypes()
            .Where(IsTenancyEndpoint)
            .ToList();

        tenancyEndpoints.Should().BeEquivalentTo(DeclaredPermissions.Keys,
            "the endpoint types reflection finds and the ones this test accounts for must be one and the same set, or an endpoint could be added to the tenancy surface with no permission pinned to it");

        foreach (var endpointType in tenancyEndpoints)
        {
            routed.Should().ContainKey(endpointType,
                "{0} must be routed for the permission it declares to be enforced at all",
                endpointType.Name);

            var declared = DeclaredPermissions[endpointType];

            if (declared is null)
            {
                routed[endpointType].AllowedPermissions.Should().BeNullOrEmpty(
                    "{0} is how a caller acquires the tenant it acts in, so authentication is its gate and it declares no permission",
                    endpointType.Name);
            }
            else
            {
                routed[endpointType].AllowedPermissions.Should().Equal([declared],
                    "{0} must require exactly {1}",
                    endpointType.Name, declared);
            }
        }
    }

    /// <summary>
    /// Verifies that a caller holding only <see cref="Allow.Tenant_View"/> is refused every operation
    /// that requires anything else, and admitted the two that require exactly that - while the same
    /// request from a caller holding the permission is carried out, so the refusal is the caller's
    /// authority rather than the request (AC-045).
    /// </summary>
    /// <param name="endpoint">The endpoint under test, one row per gated endpoint of the surface.</param>
    /// <remarks>
    /// Each row is a request that is valid in every other respect: it names a tenant the test made, a
    /// member of it, an account belonging to none, and values that pass every field rule. Nothing about
    /// the request can explain the 403, which leaves the endpoint's declared permission as the only
    /// thing that can - and the control half proves it, by having a caller who holds that permission
    /// send the identical request to the identical resource and be answered.
    /// </remarks>
    [Theory]
    [MemberData(nameof(GatedEndpointNames))]
    public async Task Missing_Permission_Is_Forbidden(string endpoint)
    {
        var call = await ArrangeAsync(endpoint);
        var limited = await ClientForAsync(LimitedUsername);

        var refused = await call.Send(limited);

        if (Declaration(endpoint) == Allow.Tenant_View)
        {
            refused.Status.Should().NotBe(HttpStatusCode.Forbidden,
                "{0} requires the one permission the purpose-built role holds, so this caller is inside its gate rather than outside it",
                endpoint);
        }
        else
        {
            refused.Status.Should().Be(HttpStatusCode.Forbidden,
                "{0} requires a permission the purpose-built role does not hold",
                endpoint);

            // The status alone would read the same for a refusal of any kind, so the code is what shows
            // this one is the caller's authority and not its tenant or its account.
            refused.Problem!.Errors.First().Code.Should().Be(ErrorCodes.PermissionDenied,
                "{0} turns a caller away for the permission it does not hold, and says so rather than answering blank (AC-045)",
                endpoint);

            // The refusal has to be the gate's: a handler that ran and refused would have done so with
            // its own answer, and this is what shows it was never reached.
            if (call.NothingHappened is { } assertNothingHappened)
            {
                await assertNothingHappened();
            }
        }

        await SetPlatformAdminAuthTokenAsync();

        var admitted = await call.Send(App.Client);

        admitted.Status.Should().NotBe(HttpStatusCode.Forbidden,
            "the very same request to the very same resource is answered for a caller that holds the permission {0} requires",
            endpoint);
    }

    /// <summary>
    /// Verifies that a purpose-built role holding exactly one permission is admitted exactly by the
    /// endpoints declaring that permission, and that widening the role by one more permission brings
    /// exactly one more endpoint into reach - so what turns a caller away is the endpoint's own
    /// declaration, which is unprovable with a role that holds everything (AC-088).
    /// </summary>
    /// <remarks>
    /// The role is read back from the database before anything is asserted with it, because a fixture
    /// role that had grown a second permission would make every refusal below read the same whether the
    /// endpoint declared anything or not. The caller and the tenant are made here rather than seeded, so
    /// the authority under test is one the test put in place and can change.
    /// </remarks>
    [Fact]
    public async Task Purpose_Built_Role_Proves_The_Gate()
    {
        var gateRoleId = TestRoles.LimitedTenantRoleId;

        (await GrantedPermissionsAsync(gateRoleId)).Should().Equal([Allow.Tenant_View],
            "the gate is only as good as the role proving it: it must hold exactly the one permission whose endpoints are expected to admit its holder");

        var tenant = await CreateTenantAsync();
        var viewRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_View);
        var member = await CreateTenantUserAsync(tenant.Id, viewRoleId);

        await SignInAsAsync(member.Username, tenant.Id);

        // Admitted: precisely what the role's one permission declares.
        var (listed, _) = await App.Client
            .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(new() { All = true });

        listed.StatusCode.Should().Be(HttpStatusCode.OK,
            "the list declares Tenant.View, which is what the purpose-built role was given it for");

        var (read, _) = await App.Client
            .GETAsync<TenantGetEndpoint, TenantGetRequest, TenantGetResponse>(new() { Id = tenant.Id });

        read.StatusCode.Should().Be(HttpStatusCode.OK, "as does the read");

        // Refused: and refused at the gate, so the tenant is exactly as it stood before the request.
        var (suspended, _) = await App.Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, TenantSuspendResponse>(new() { Id = tenant.Id });

        suspended.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "suspending declares Tenant.Suspend, which the role does not hold");
        (await LiveTenantAsync(tenant.Id)).Status.Should().Be(TenantStatus.Active,
            "a caller outside the gate never reaches the handler, so nothing was written");

        var (renamed, _) = await App.Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, TenantUpdateResponse>(
                new() { Id = tenant.Id, Name = "Renamed By A Gate Test", Identifier = tenant.Identifier });

        renamed.StatusCode.Should().Be(HttpStatusCode.Forbidden, "renaming declares Tenant.Update, which the role does not hold");
        (await LiveTenantAsync(tenant.Id)).Name.Should().Be(tenant.Name, "so the rename never happened");

        // The proof itself: one permission added to the caller's roles, and the endpoint that permission
        // declares answers - while the one before it still does not.
        var updateRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Tenant_Update);
        await TenantAuthorizationService.ReplaceTenantRoleAssignmentsAsync(
            tenant.Id, member.Id, [viewRoleId, updateRoleId], TestContext.Current.CancellationToken);

        // Signed in again so the session is rebuilt around the roles as they now stand, rather than
        // resting on when a permission recomputation happens to fall.
        await SignInAsAsync(member.Username, tenant.Id);

        var (nowRenamed, _) = await App.Client
            .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, TenantUpdateResponse>(
                new() { Id = tenant.Id, Name = "Renamed By A Gate Test", Identifier = tenant.Identifier });

        nowRenamed.StatusCode.Should().Be(HttpStatusCode.OK,
            "the request refused a moment ago is answered the moment the caller holds Tenant.Update, and by nothing else");

        (await LiveTenantAsync(tenant.Id)).Name.Should().Be("Renamed By A Gate Test", "and the handler did run this time");

        var (stillRefused, _) = await App.Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, TenantSuspendResponse>(new() { Id = tenant.Id });

        stillRefused.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "while Tenant.Suspend is still not held: the gate opens one endpoint at a time, following the declarations");
    }

    /// <summary>
    /// The endpoint type a theory row names, so a row and the map above can be matched without the
    /// theory having to carry a type the test runner cannot serialize.
    /// </summary>
    /// <param name="endpoint">The endpoint type's name.</param>
    /// <returns>The endpoint type.</returns>
    private static Type EndpointNamed(string endpoint)
        => DeclaredPermissions.Keys.Single(type => type.Name == endpoint);

    /// <summary>
    /// The permission one endpoint declares, read from the single map this test is written around.
    /// </summary>
    /// <param name="endpoint">The endpoint type's name.</param>
    /// <returns>The permission it declares, or <see langword="null"/> when it declares none.</returns>
    private static string? Declaration(string endpoint) => DeclaredPermissions[EndpointNamed(endpoint)];

    /// <summary>
    /// Whether a type is one of the tenancy surface's endpoints: a concrete class in the feature's
    /// endpoint namespace, named the way every endpoint in this codebase is.
    /// </summary>
    /// <param name="type">The type to judge.</param>
    /// <returns><see langword="true"/> when it is an endpoint of the tenancy surface.</returns>
    private static bool IsTenancyEndpoint(Type type)
        => type is { IsClass: true, IsAbstract: false }
           && (type.Namespace == TenancyEndpointNamespace
               || type.Namespace?.StartsWith($"{TenancyEndpointNamespace}.", StringComparison.Ordinal) == true)
           && type.Name.EndsWith("Endpoint", StringComparison.Ordinal);

    /// <summary>
    /// What an endpoint answered: the status, and the problem details it was refused with, when it was
    /// refused.
    /// </summary>
    /// <param name="Status">The status code of the answer.</param>
    /// <param name="Problem">The refusal body, or <see langword="null"/> when the request was not refused.</param>
    private sealed record Answer(HttpStatusCode Status, ProblemDetails? Problem);

    /// <summary>
    /// The status an endpoint answered with, and the refusal it carried when there was one. The body is
    /// read only for a refusal, because a refusal is the only answer whose contents are asserted: what a
    /// refusal says - and which code it says it with - is the error-code half of the criterion.
    /// </summary>
    /// <typeparam name="TResponse">The endpoint's own response type.</typeparam>
    /// <param name="call">The call to await.</param>
    /// <returns>The answer, with the refusal body when the request was refused.</returns>
    private static async Task<Answer> StatusOf<TResponse>(Task<TestResult<TResponse>> call)
    {
        var (response, _) = await call;

        if (response.StatusCode != HttpStatusCode.Forbidden)
        {
            return new(response.StatusCode, null);
        }

        var problem = await response.Content
            .ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);

        return new(response.StatusCode, problem);
    }

    /// <summary>
    /// The live endpoint definitions the framework routes to, keyed by the endpoint type they belong
    /// to - the map endpoint authorization actually reads.
    /// </summary>
    /// <returns>One definition per routed endpoint.</returns>
    private Dictionary<Type, EndpointDefinition> LiveEndpointDefinitions()
        => App.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(route => route.Metadata.GetMetadata<EndpointDefinition>())
            .OfType<EndpointDefinition>()
            .DistinctBy(definition => definition.EndpointType)
            .ToDictionary(definition => definition.EndpointType);

    /// <summary>
    /// One endpoint's request, together with the evidence that a refused request never reached the
    /// handler - the only thing that tells a gate which turned the request away from a handler that ran
    /// and refused it.
    /// </summary>
    /// <param name="Send">Issues the request as the client's caller and reports the answer it got.</param>
    /// <param name="NothingHappened">Asserts the resource is exactly as the request found it, or <see langword="null"/> for a request that reads.</param>
    private sealed record Call(Func<HttpClient, Task<Answer>> Send, Func<Task>? NothingHappened = null);

    /// <summary>
    /// Builds the tenant, the members and the values one endpoint's request is sent against, and
    /// returns that request. Everything is made by the test, so the control half of the theory is free
    /// to carry the request out: the tenant it changes is one nobody else reads.
    /// </summary>
    /// <param name="endpoint">The endpoint whose arrangement is wanted.</param>
    /// <returns>The request to send, and what has to have been left alone when it is refused.</returns>
    private async Task<Call> ArrangeAsync(string endpoint)
    {
        // A tenant of the test's own, born in the state the row needs: reactivation has nothing to
        // prove on a tenant that was never suspended.
        var tenant = endpoint == nameof(TenantReactivateEndpoint)
            ? await CreateTenantAsync(TenantStatus.Suspended)
            : await CreateTenantAsync();

        // The member holding the tenant's administration, so a removal or a role replacement has a
        // target that leaves the tenant administered either way.
        var administratorRoleId = await TenantAdministratorRoleIdAsync(tenant.Id);
        await CreateTenantUserAsync(tenant.Id, administratorRoleId);

        var removable = await CreateTenantUserAsync(tenant.Id);
        var outsider = await CreateAccountWithoutMembershipAsync();
        var candidateName = "Tenant Created By A Permission Test";
        var candidateIdentifier = NewTenantIdentifier();
        var updatedName = "Tenant Renamed By A Permission Test";
        var updatedIdentifier = NewTenantIdentifier();

        return endpoint switch
        {
            nameof(TenantCreateEndpoint) => new(
                async client => await StatusOf(client
                    .POSTAsync<TenantCreateEndpoint, TenantCreateRequest, TenantCreateResponse>(
                        new() { Name = candidateName, Identifier = candidateIdentifier })),
                async () => (await AllTenantsAsync()).Should()
                    .NotContain(row => row.IdentifierNormalized == candidateIdentifier,
                        "a refused creation writes no tenant at all")),

            nameof(TenantUpdateEndpoint) => new(
                async client => await StatusOf(client
                    .PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, TenantUpdateResponse>(
                        new() { Id = tenant.Id, Name = updatedName, Identifier = updatedIdentifier })),
                async () => (await LiveTenantAsync(tenant.Id)).Identifier.Should().Be(tenant.Identifier,
                    "a refused rename leaves the identifier the tenant was known by")),

            nameof(TenantDeleteEndpoint) => new(
                async client => await StatusOf(client
                    .DELETEAsync<TenantDeleteEndpoint, TenantDeleteRequest, TenantDeleteResponse>(new() { Id = tenant.Id })),
                async () => (await LiveTenantAsync(tenant.Id)).IsDeleted.Should().BeFalse(
                    "a refused deletion retains its row rather than soft-deleting it")),

            nameof(TenantGetEndpoint) => new(
                async client => await StatusOf(client
                    .GETAsync<TenantGetEndpoint, TenantGetRequest, TenantGetResponse>(new() { Id = TestTenants.BootstrapTenantId }))),

            nameof(TenantListEndpoint) => new(
                async client => await StatusOf(client
                    .GETAsync<TenantListEndpoint, TenantListRequest, TenantListResponse>(new() { All = true }))),

            nameof(TenantSuspendEndpoint) => new(
                async client => await StatusOf(client
                    .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, TenantSuspendResponse>(new() { Id = tenant.Id })),
                async () => (await LiveTenantAsync(tenant.Id)).Status.Should().Be(tenant.Status,
                    "a refused suspension leaves the tenant in service")),

            nameof(TenantReactivateEndpoint) => new(
                async client => await StatusOf(client
                    .POSTAsync<TenantReactivateEndpoint, TenantReactivateRequest, TenantReactivateResponse>(new() { Id = tenant.Id })),
                async () => (await LiveTenantAsync(tenant.Id)).Status.Should().Be(TenantStatus.Suspended,
                    "a refused reactivation leaves the tenant exactly as it was found")),

            nameof(TenantMemberListEndpoint) => new(
                async client => await StatusOf(client
                    .GETAsync<TenantMemberListEndpoint, TenantMemberListRequest, TenantMemberListResponse>(
                        new() { TenantId = tenant.Id, All = true }))),

            nameof(TenantMemberAddEndpoint) => new(
                async client => await StatusOf(client
                    .POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, TenantMemberAddResponse>(
                        new() { TenantId = tenant.Id, UserId = outsider.Id, Roles = [administratorRoleId] })),
                async () => (await MembershipRowsAsync(tenant.Id, outsider.Id)).Should().BeEmpty(
                    "a refused addition writes no membership for the account it named")),

            nameof(TenantMemberRemoveEndpoint) => new(
                async client => await StatusOf(client
                    .DELETEAsync<TenantMemberRemoveEndpoint, TenantMemberRemoveRequest, TenantMemberRemoveResponse>(
                        new() { TenantId = tenant.Id, UserId = removable.Id })),
                async () => (await MembershipRowsAsync(tenant.Id, removable.Id)).Should()
                    .ContainSingle(membership => !membership.IsDeleted,
                        "a refused removal leaves the membership as it stood")),

            nameof(TenantMemberUpdateRolesEndpoint) => new(
                async client => await StatusOf(client
                    .PUTAsync<TenantMemberUpdateRolesEndpoint, TenantMemberUpdateRolesRequest, TenantMemberUpdateRolesResponse>(
                        new() { TenantId = tenant.Id, UserId = removable.Id, Roles = [administratorRoleId] })),
                async () => (await GrantedRolesAsync(tenant.Id, removable.Id)).Should().BeEmpty(
                    "a refused role replacement grants the roles it named nowhere")),

            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint,
                "every gated endpoint of the tenancy surface has to have an arrangement, or this theory would pass by sending nothing")
        };
    }

    /// <summary>
    /// Every tenant retained in the database, deleted ones included, so a test can tell a tenant that
    /// was never written from one that was removed.
    /// </summary>
    /// <returns>The retained tenant rows.</returns>
    private async Task<List<Tenant>> AllTenantsAsync()
        => await DbContext.Tenants
            .AcrossAllTenants()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// One tenant as the database now holds it, deleted rows included, read afresh rather than from
    /// anything the test is tracking.
    /// </summary>
    /// <param name="tenantId">The tenant to read.</param>
    /// <returns>The tenant row.</returns>
    private async Task<Tenant> LiveTenantAsync(Guid tenantId)
        => await DbContext.Tenants
            .AcrossAllTenants()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .AsNoTracking()
            .SingleAsync(tenant => tenant.Id == tenantId, TestContext.Current.CancellationToken);

    /// <summary>
    /// The identity of the role a tenant was provisioned with - the one holding tenant administration -
    /// read from the tenant's own system-created role rather than from a name.
    /// </summary>
    /// <param name="tenantId">The tenant whose administrator role is read.</param>
    /// <returns>The identifier of that role.</returns>
    private async Task<Guid> TenantAdministratorRoleIdAsync(Guid tenantId)
        => await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId && role.SystemCreated)
            .Select(role => role.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// The permission names one role grants, read from the role's own assignments so that what a test
    /// proves with a role is what the database says the role holds.
    /// </summary>
    /// <param name="roleId">The role to read.</param>
    /// <returns>The permission names it grants.</returns>
    private async Task<List<string>> GrantedPermissionsAsync(Guid roleId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var permissionIds = await DbContext.RolePermissions
            .Where(link => link.RoleId == roleId)
            .Select(link => link.PermissionId)
            .ToListAsync(cancellationToken);

        return await DbContext.Permissions
            .Where(permission => permissionIds.Contains(permission.Id))
            .Select(permission => permission.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The roles an account holds inside one named tenant, read from the assignments rather than from
    /// anything an endpoint reported.
    /// </summary>
    /// <param name="tenantId">The tenant the roles are read for.</param>
    /// <param name="userId">The account whose roles are read.</param>
    /// <returns>The identifiers of the roles it holds there.</returns>
    /// <remarks>
    /// The tenant's role identifiers are read first and stated in the predicate, because the
    /// assignments themselves are not tenant-scoped - a role carries the tenant, not the link that
    /// grants it - and a predicate that reached through <c>Role</c> would be filtering on a set the
    /// active scope has already narrowed.
    /// </remarks>
    private async Task<List<Guid>> GrantedRolesAsync(Guid tenantId, Guid userId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var tenantRoleIds = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId)
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        return await DbContext.UserRoles
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId && tenantRoleIds.Contains(assignment.RoleId))
            .Select(assignment => assignment.RoleId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Every membership row recorded for one account in one tenant, removed ones included, so a test
    /// can tell a membership that was never written from one that was withdrawn.
    /// </summary>
    /// <param name="tenantId">The tenant the membership belongs to.</param>
    /// <param name="userId">The account the membership belongs to.</param>
    /// <returns>The retained membership rows for that pair.</returns>
    private async Task<List<TenantMembership>> MembershipRowsAsync(Guid tenantId, Guid userId)
        => await DbContext.TenantMemberships
            .AcrossAllTenants()
            .IgnoreQueryFilters([SoftDeleteFilterKey])
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId && membership.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
}
