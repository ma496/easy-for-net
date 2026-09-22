namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using System.Text;
using Backend.ShareData.Entities;
using Backend.Features.FileManagement.Core;
using Backend.Features.FileManagement.Endpoints.Files;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Tests for what suspension does to the members of a tenant: that a tenant-scoped request stops
/// being answered, that the caller keeps their session and is offered another tenant instead of being
/// signed out, that each refusal names what actually went wrong, that the tenant's files stop being
/// served while being retained, and that reactivation restores access on the token that was refused
/// (AC-007, AC-008, AC-029, AC-060, AC-070, AC-089).
/// </summary>
/// <remarks>
/// <para>
/// Suspension is enforced where a session is minted rather than by any endpoint, so this class tests
/// the enforcement by what a member of a suspended tenant gets back rather than by calling anything of
/// the tenant's: a tenant-scoped call that was answered before suspension stops being answered once
/// the session is renewed. The caller is never signed out - the renewal succeeds and simply hands back
/// a session that names no tenant - so what they lose is that tenant's authority and not their
/// identity, which is what lets the application offer them another tenant rather than a sign-in
/// screen.
/// </para>
/// <para>
/// The renewal is the point. What a session may do is decided when its token is minted and trusted
/// until that token is replaced, so an access token issued before the suspension keeps working until
/// it is renewed. Every case below therefore renews before asserting the refusal, which is the moment
/// the change actually reaches a live session.
/// </para>
/// <para>
/// Every tenant, role, membership, account and file a test asserts on is made by the test itself.
/// Suspension is applied to those created tenants and to no seeded one, so the class never takes the
/// bootstrap tenant or a seeded account out of service for the rest of the suite
/// <see cref="TenantSuspendTests"/> covers the endpoint that performs the suspension itself.
/// </para>
/// </remarks>
public class TenantSuspensionTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a member of a suspended tenant stops being answered once their session is
    /// renewed: the renewal succeeds, hands back a session naming no tenant, and every tenant-scoped
    /// call is refused from then on - a 403 rather than the 200 it answered before or the 401 of a
    /// session that ended (AC-007).
    /// </summary>
    [Fact]
    public async Task Suspended_Tenant_Stops_Answering_Once_The_Session_Is_Renewed()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var session = await SessionForAsync(member.Username, tenant.Id);

        // Answered while the tenant is in service, so the refusal that follows is provably caused by
        // the suspension rather than by the request never having been admissible.
        var (before, page) = await session.Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        before.StatusCode.Should().Be(HttpStatusCode.OK, "the member holds the permission the endpoint requires, and the tenant is in service");
        page.Should().NotBeNull();

        await SetPlatformAdminAuthTokenAsync();
        await SuspendTenantAsync(tenant.Id);

        // The token issued before the suspension carries the tenant's authority until it is replaced,
        // which is the bound this design accepts: the change reaches the session at its next renewal.
        await session.RenewAsync();

        var (after, refusal) = await session.Client.GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        after.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the tenant being out of service is a refusal of the operation, not of the caller's identity");
        after.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "the caller stays signed in and is asked for no credentials");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.PermissionDenied,
            "the renewed session carries no tenant and therefore no permission, so what refuses the call is the authority it lacks");
    }

    /// <summary>
    /// Verifies that suspending one tenant leaves its member's session standing and their other
    /// memberships intact: the same token is refused the suspended tenant and still gets an answer
    /// naming the tenant they may work in instead, so the caller is never left signed out and never
    /// left without somewhere to go (AC-029).
    /// </summary>
    [Fact]
    public async Task Suspension_Keeps_The_Session_And_Offers_Another_Tenant()
    {
        var suspended = await CreateTenantAsync();
        var remaining = await CreateTenantAsync();
        var member = await CreateTenantUserAsync(suspended.Id, await CreateTenantRoleAsync(suspended.Id, Allow.User_View));
        await AddMembershipAsync(remaining.Id, member.Id);

        var session = await SessionForAsync(member.Username, suspended.Id);

        await SetPlatformAdminAuthTokenAsync();
        await SuspendTenantAsync(suspended.Id);

        // The renewal is what the suspension reaches, and it succeeds: ending the session would sign
        // the caller out over something that was not their doing.
        await session.RenewAsync();

        var (refused, refusal) = await session.Client.GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        refusal.Errors.First().Code.Should().Be(ErrorCodes.PermissionDenied);

        // The same token, on an endpoint that has to answer a caller with no usable tenant: this is
        // how the application learns which tenant to offer instead, so a refusal here would leave the
        // caller with nowhere to go.
        var (answered, info) = await session.Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        answered.StatusCode.Should().Be(HttpStatusCode.OK, "the session survives the tenant being suspended");
        info.Tenants.Select(tenant => tenant.Id).Should().Contain(remaining.Id, "the membership that was not suspended is still offered");
        info.Tenants.Select(tenant => tenant.Id).Should().NotContain(suspended.Id, "a tenant that cannot be worked in is not offered as a choice");
        info.ActiveTenantId.Should().BeNull("the renewal dropped the suspended tenant, so there is none to report");
        info.ActiveTenant.Should().BeNull();
    }

    /// <summary>
    /// Verifies that both ways a session's tenant can stop being usable - the tenant going out of
    /// service, and the membership being removed - leave the caller signed in with the tenants they
    /// still belong to, rather than signing them out (AC-070).
    /// </summary>
    /// <remarks>
    /// The two causes are no longer told apart by the refusal, and deliberately so: a renewed session
    /// simply names no tenant, so what refuses the next call is the authority it lacks, the same way it
    /// would for any caller short of a permission. What tells the caller where to go next is the
    /// account info endpoint, which names the tenants that are still theirs - and that is asserted
    /// here for both causes.
    /// </remarks>
    [Fact]
    public async Task Losing_A_Tenant_Keeps_The_Session_And_Offers_What_Remains()
    {
        // Two accounts rather than one, because a single account would have to belong to three tenants
        // to keep an alternative after one is suspended and another is revoked, and the two causes are
        // what is being compared here - not their combination.
        var suspendedTenant = await CreateTenantAsync();
        var suspendedAlternative = await CreateTenantAsync();
        var suspendedMember = await CreateTenantUserAsync(
            suspendedTenant.Id,
            await CreateTenantRoleAsync(suspendedTenant.Id, Allow.User_View));
        await AddMembershipAsync(suspendedAlternative.Id, suspendedMember.Id);

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

        // Both renewals succeed: neither cause ends a session, and a renewal that failed would sign the
        // caller out over something that was not their doing.
        await suspendedSession.RenewAsync();
        await revokedSession.RenewAsync();

        var (suspendedResponse, suspendedRefusal) = await suspendedSession.Client
            .GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());
        var (revokedResponse, revokedRefusal) = await revokedSession.Client
            .GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        suspendedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden, "a suspended tenant is a refusal, never a sign-out");
        revokedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden, "so is a membership that was removed");

        suspendedRefusal.Errors.First().Code.Should().Be(ErrorCodes.PermissionDenied);
        revokedRefusal.Errors.First().Code.Should().Be(ErrorCodes.PermissionDenied);

        // Where the caller goes next is answered here rather than by the refusal: each is still signed
        // in, and each is offered the tenant they still belong to.
        var (suspendedInfoResponse, suspendedInfo) = await suspendedSession.Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();
        var (revokedInfoResponse, revokedInfo) = await revokedSession.Client.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        suspendedInfoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        revokedInfoResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        suspendedInfo.ActiveTenantId.Should().BeNull("the renewal dropped the suspended tenant");
        revokedInfo.ActiveTenantId.Should().BeNull("and the one whose membership was removed");

        suspendedInfo.Tenants.Select(tenant => tenant.Id).Should().Contain(suspendedAlternative.Id);
        revokedInfo.Tenants.Select(tenant => tenant.Id).Should().Contain(revokedAlternative.Id);
    }

    /// <summary>
    /// Verifies that a file uploaded in a tenant stops being served once that tenant is suspended,
    /// while the record attributing it and the stored bytes themselves are retained - so suspension
    /// withdraws access without destroying data, and reactivation has something to restore
    /// (AC-060).
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

        var (downloadResponse, refusal) = await memberClient
            .GETAsync<FileGetEndpoint, FileGetRequest, ProblemDetails>(new() { FileName = uploaded.FileName });

        downloadResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the refusal names suspension rather than reporting the file as missing");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.TenantSuspended);

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
    /// Verifies that reactivating a tenant returns its members to work without a second sign-in - the
    /// other half of the refusal being a refusal of the operation rather than of the session
    /// (AC-008, AC-089).
    /// </summary>
    /// <remarks>
    /// The member signs in once and never again: the session that lost the tenant at one renewal gets
    /// it back at the next, because every renewal reads the tenant as it stands rather than remembering
    /// what the last one decided.
    /// </remarks>
    [Fact]
    public async Task Reactivation_Restores_Member_Access()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var session = await SessionForAsync(member.Username, tenant.Id);

        await SetPlatformAdminAuthTokenAsync();
        await SuspendTenantAsync(tenant.Id);
        await session.RenewAsync();

        var (refused, _) = await session.Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Reactivated by the platform surface, while the member's session is renewed once more rather
        // than signed in again.
        await Client.POSTAsync<TenantReactivateEndpoint, TenantReactivateRequest, TenantReactivateResponse>(
            new() { Id = tenant.Id });

        await session.RenewAsync();

        var (restored, page) = await session.Client.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        restored.StatusCode.Should().Be(HttpStatusCode.OK, "the member is admitted again without re-entering credentials");
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
