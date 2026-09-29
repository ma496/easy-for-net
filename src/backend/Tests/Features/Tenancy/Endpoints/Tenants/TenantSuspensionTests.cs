namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using System.Text;
using Backend.ShareData.Entities;
using Backend.Tests.Features.Identity;
using Backend.Features.FileManagement.Core;
using Backend.Features.FileManagement.Endpoints.Files;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Tests for what suspension does to the members of a tenant: that a tenant-scoped request stops
/// being answered because the sessions acting in it end at once, that sessions in other tenants
/// carry on, that the tenant's files stop being
/// served while being retained, and that reactivation leaves the revoked token at 401 while a fresh
/// sign-in is admitted again.
/// </summary>
/// <remarks>
/// <para>
/// Suspending a tenant ends every session acting in it: the access token, not renewed, answers 401 at
/// once and the refresh token is refused, so a member is signed out of that tenant rather than handed
/// a renewed session that names none. Sessions in other tenants and in platform scope are untouched,
/// which is what lets a member who belongs to several tenants sign in again to another one.
/// </para>
/// <para>
/// A membership withdrawn directly in the database, with no endpoint involved, ends no session; the
/// renewal is then what drops the tenant, and one case below keeps that behaviour asserted.
/// </para>
/// <para>
/// Every tenant, role, membership, account and file a test asserts on is made by the test itself.
/// Suspension is applied to those created tenants and to no seeded one, so the class never takes the
/// bootstrap tenant or a seeded account out of service for the rest of the suite
/// <see cref="TenantSuspendTests"/> covers the endpoint that performs the suspension itself.
/// </para>
/// </remarks>
public class TenantSuspensionTests(App app) : SessionRevocationTestsBase(app)
{
    /// <summary>
    /// Verifies that suspending a tenant ends its member's session at once: the access token answers
    /// 401 with no renewal, and the refresh token is refused.
    /// </summary>
    [Fact]
    public async Task Suspending_A_Tenant_Ends_Its_Members_Sessions()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var session = await SessionForAsync(member.Username, tenant.Id);

        // Answered while the tenant is in service, so the 401 that follows is provably caused by the
        // suspension rather than by the request never having been admissible.
        var (before, page) = await session.Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        before.StatusCode.Should().Be(HttpStatusCode.OK, "the member holds the permission the endpoint requires, and the tenant is in service");
        page.Should().NotBeNull();

        await SetPlatformAdminAuthTokenAsync();
        await SuspendTenantAsync(tenant.Id);

        await AssertEndedAsync(session);
    }

    /// <summary>
    /// Verifies that suspending one tenant ends only the sessions acting in it: the same account's
    /// session in another tenant keeps working, and it still lists the tenant that remains.
    /// </summary>
    [Fact]
    public async Task Suspension_Ends_The_Suspended_Tenant_Session_And_Keeps_Another_Tenant_Session()
    {
        var suspended = await CreateTenantAsync();
        var remaining = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(suspended.Id, await CreateTenantRoleAsync(suspended.Id, Allow.User_View));
        await AddMembershipAsync(remaining.Id, member.Id);

        var suspendedSession = await SessionForAsync(member.Username, suspended.Id);
        var remainingSession = await SessionForAsync(member.Username, remaining.Id);

        await SetPlatformAdminAuthTokenAsync();
        await SuspendTenantAsync(suspended.Id);

        await AssertEndedAsync(suspendedSession);
        await AssertAliveAsync(remainingSession);

        var (answered, info) = await remainingSession.Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        answered.StatusCode.Should().Be(HttpStatusCode.OK);
        info.Tenants.Select(tenant => tenant.Id).Should().Contain(remaining.Id, "the membership that was not suspended is still offered");
        info.Tenants.Select(tenant => tenant.Id).Should().NotContain(suspended.Id, "a tenant that cannot be worked in is not offered as a choice");
    }

    /// <summary>
    /// Verifies the two ways a session's tenant can stop being usable. Suspension through the endpoint
    /// ends the session at once. A membership withdrawn with no endpoint involved ends nothing, so the
    /// session's renewal is what drops the tenant and leaves the caller signed in with the tenants they
    /// still belong to.
    /// </summary>
    [Fact]
    public async Task Suspension_Ends_The_Session_While_A_Direct_Membership_Removal_Is_Dropped_At_Renewal()
    {
        var suspendedTenant = await CreateTenantAsync();
        var suspendedMember = await CreateTenantUserAsync(
            suspendedTenant.Id,
            await CreateTenantRoleAsync(suspendedTenant.Id, Allow.User_View));

        var revokedTenant = await CreateTenantAsync();
        var revokedAlternative = await CreateTenantAsync();
        var revokedMember = await CreateTenantUserAsync(
            revokedTenant.Id,
            await CreateTenantRoleAsync(revokedTenant.Id, Allow.User_View));
        await AddMembershipAsync(revokedAlternative.Id, revokedMember.Id);

        var suspendedSession = await SessionForAsync(suspendedMember.Username, suspendedTenant.Id);
        var revokedSession = await SessionForAsync(revokedMember.Username, revokedTenant.Id);

        await SetPlatformAdminAuthTokenAsync();
        await SuspendTenantAsync(suspendedTenant.Id);
        await RevokeMembershipAsync(revokedTenant.Id, revokedMember.Id);

        await AssertEndedAsync(suspendedSession);

        // The row was removed under the session with no revocation, so the session is still alive and
        // its renewal - which reads the membership as it stands - is what drops the tenant.
        await revokedSession.RenewAsync();

        var (revokedResponse, revokedRefusal) = await revokedSession.Client
            .GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        revokedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden, "a membership removed without revocation is a refusal, never a sign-out");
        revokedRefusal.Errors.First().Code.Should().Be(ErrorCodes.PermissionDenied.Value);

        var (revokedInfoResponse, revokedInfo) = await revokedSession.Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        revokedInfoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        revokedInfo.ActiveTenantId.Should().BeNull("the renewal dropped the tenant whose membership was removed");
        revokedInfo.Tenants.Select(tenant => tenant.Id).Should().Contain(revokedAlternative.Id);
    }

    /// <summary>
    /// Verifies that a file uploaded in a tenant stops being served once that tenant is suspended,
    /// while the record attributing it and the stored bytes themselves are retained - so suspension
    /// withdraws access without destroying data, and reactivation has something to restore.
    /// </summary>
    [Fact]
    public async Task Suspended_Tenant_Files_Are_Not_Served_But_Are_Retained()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var memberClient = await ClientForAsync(member.Username, tenant.Id);

        var content = "the tenant's own file";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var upload = new FileUploadRequest
        {
            File = new FormFile(stream, 0, stream.Length, "file", "notes.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            },
            AccountOwned = false
        };

        var (uploadResponse, uploaded) = await memberClient
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, FileUploadResponse>(upload, sendAsFormData: true);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK, "the upload acts in the tenant the member's session names");
        uploaded.FileName.Should().NotBeNullOrWhiteSpace();

        var storageProvider = Service<IStorageProvider>();
        storageProvider.Exists(uploaded.FileName).Should().BeTrue("the content reached storage");

        await SetPlatformAdminAuthTokenAsync();
        await SuspendTenantAsync(tenant.Id);

        var (downloadResponse, _) = await memberClient
            .GETAsync<FileGetEndpoint, FileGetRequest, ProblemDetails>(new() { FileName = uploaded.FileName });

        downloadResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the member's session was ended by the suspension, so nothing is served to it");

        // A session the suspension did not end - the platform administrator's, in platform scope -
        // reaches the file lookup itself, which refuses on the owning tenant's state.
        var (refused, refusal) = await Client
            .GETAsync<FileGetEndpoint, FileGetRequest, ProblemDetails>(new() { FileName = uploaded.FileName });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.TenantSuspended.Value,
            "the refusal names the state of the tenant that owns the file");

        var raw = await Client.GetAsync($"/api/file-management/{Uri.EscapeDataString(uploaded.FileName)}", TestContext.Current.CancellationToken);

        (await raw.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain(content,
            "not one byte of a suspended tenant's file is served");

        // Retained, not destroyed: the record still attributes the file to the tenant, and the bytes
        // are still in storage. A suspension that deleted either would make reactivation a rebuild.
        var storedFile = await DbContext.StoredFiles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(file => file.FileName == uploaded.FileName, TestContext.Current.CancellationToken);

        storedFile.TenantId.Should().Be(tenant.Id, "the file stays attributed to the tenant it was uploaded in");
        storedFile.OriginalFileName.Should().Be("notes.txt");
        storageProvider.Exists(uploaded.FileName).Should().BeTrue("the content is retained while the tenant is out of service");
    }

    /// <summary>
    /// Verifies that reactivating a tenant returns its members to work: the session the suspension
    /// ended stays ended, and signing in again is admitted to the tenant.
    /// </summary>
    [Fact]
    public async Task Reactivation_Restores_Member_Access()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var session = await SessionForAsync(member.Username, tenant.Id);

        await SetPlatformAdminAuthTokenAsync();
        await SuspendTenantAsync(tenant.Id);

        await AssertEndedAsync(session);

        await Client.POSTAsync<TenantReactivateEndpoint, TenantReactivateRequest, TenantReactivateResponse>(
            new() { Id = tenant.Id });

        (await InfoStatusAsync(session.Client)).Should().Be(HttpStatusCode.Unauthorized, "reactivation does not resurrect a revoked session");

        var signedInAgain = await SessionForAsync(member.Username, tenant.Id);

        var (restored, page) = await signedInAgain.Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        restored.StatusCode.Should().Be(HttpStatusCode.OK, "the member is admitted again on a new sign-in");
        page.Should().NotBeNull();
    }

    /// <summary>
    /// Suspends a tenant through the platform surface, so the operation is performed the one way it is
    /// performed in production and the tests are not asserting on a state they arranged themselves.
    /// </summary>
    /// <param name="tenantId">The tenant to put out of service.</param>
    private async Task SuspendTenantAsync(Guid tenantId)
    {
        var (response, _) = await Client
            .POSTAsync<TenantSuspendEndpoint, TenantSuspendRequest, TenantSuspendResponse>(new() { Id = tenantId });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "a test suspending a tenant it created must actually have suspended it");
    }

    /// <summary>
    /// Joins an account to a second tenant, writing the membership row the way account creation does -
    /// inside the tenant being joined, so save-time attribution stamps it with that tenant.
    /// </summary>
    /// <param name="tenantId">The tenant the account is to join.</param>
    /// <param name="userId">The account joining it.</param>
    private async Task AddMembershipAsync(Guid tenantId, Guid userId)
        => await TenantScopedAsync(tenantId, async () =>
        {
            DbContext.TenantMemberships.Add(new TenantMembership { UserId = userId });
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

    /// <summary>
    /// Removes an account's membership of a tenant, read and written inside that tenant's own scope so
    /// the row is addressed by the scope rather than by a filter that has to be relaxed to find it.
    /// The row is soft-deleted, which is what leaves the account still belonging to its other tenants.
    /// </summary>
    /// <param name="tenantId">The tenant the account is to be removed from.</param>
    /// <param name="userId">The account being removed.</param>
    private async Task RevokeMembershipAsync(Guid tenantId, Guid userId)
        => await TenantScopedAsync(tenantId, async () =>
        {
            var membership = await DbContext.TenantMemberships
                .SingleAsync(row => row.UserId == userId, TestContext.Current.CancellationToken);

            DbContext.TenantMemberships.Remove(membership);
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
}
