namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

using System.Text;
using Backend.Data.Entities;
using Backend.Features.FileManagement.Core;
using Backend.Features.FileManagement.Endpoints.Files;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Identity.Endpoints.Users;
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
/// Suspension is enforced centrally rather than by any endpoint, so this class tests the enforcement
/// by what a member of a suspended tenant gets back rather than by calling anything of the tenant's:
/// a tenant-scoped call that was answered before suspension is refused after it, on the token that was
/// already issued and without a second sign-in. The refusals are 403 and never 401, and each carries
/// the code naming its own cause - which is what lets the application offer the caller another tenant
/// rather than a sign-in screen.
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
    /// Verifies that a tenant-scoped request made by a member of a suspended tenant is refused for the
    /// tenant being out of service - a 403 naming suspension rather than the 200 it answered before or
    /// the 401 of a session that ended (AC-007).
    /// </summary>
    [Fact]
    public async Task Suspended_Tenant_Refuses_Tenant_Scoped_Requests()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var memberClient = await ClientForAsync(member.Username, tenant.Id);

        // Answered while the tenant is in service, so the refusal that follows is provably caused by
        // the suspension rather than by the request never having been admissible.
        var (before, page) = await memberClient.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        before.StatusCode.Should().Be(HttpStatusCode.OK, "the member holds the permission the endpoint requires, and the tenant is in service");
        page.Should().NotBeNull();

        await SetPlatformAdminAuthTokenAsync();
        await SuspendTenantAsync(tenant.Id);

        var (after, refusal) = await memberClient.GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        after.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the tenant being out of service is a refusal of the operation, not of the caller's identity");
        after.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "the caller stays signed in and is asked for no credentials");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.TenantSuspended, "the refusal names the tenant being suspended rather than the permission the caller no longer holds there");
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

        var memberClient = await ClientForAsync(member.Username, suspended.Id);

        await SetPlatformAdminAuthTokenAsync();
        await SuspendTenantAsync(suspended.Id);

        var (refused, refusal) = await memberClient.GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        refusal.Errors.First().Code.Should().Be(ErrorCodes.TenantSuspended);

        // The same token, on an endpoint that has to answer a caller with no usable tenant: this is
        // how the application learns which tenant to offer instead, so a refusal here would leave the
        // caller with nowhere to go.
        var (answered, info) = await memberClient.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        answered.StatusCode.Should().Be(HttpStatusCode.OK, "the session survives the tenant being suspended");
        info.Tenants.Select(tenant => tenant.Id).Should().Contain(remaining.Id, "the membership that was not suspended is still offered");
        info.Tenants.Select(tenant => tenant.Id).Should().NotContain(suspended.Id, "a tenant that cannot be worked in is not offered as a choice");
        info.ActiveTenantId.Should().BeNull("the selection went stale with the suspension and is discarded rather than reported back");
        info.ActiveTenant.Should().BeNull();
    }

    /// <summary>
    /// Verifies that each way a session's tenant can stop being usable is reported as its own distinct
    /// cause with a 403 rather than a 401, while the session itself keeps answering for the tenants the
    /// caller still belongs to - the difference between a tenant out of service and a membership that
    /// was removed, told apart by what the caller is told (AC-070).
    /// </summary>
    [Fact]
    public async Task Refusal_Explains_And_Keeps_The_Session()
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

        var suspendedClient = await ClientForAsync(suspendedMember.Username, suspendedTenant.Id);
        var revokedClient = await ClientForAsync(revokedMember.Username, revokedTenant.Id);

        await SetPlatformAdminAuthTokenAsync();
        await SuspendTenantAsync(suspendedTenant.Id);
        await RevokeMembershipAsync(revokedTenant.Id, revokedMember.Id);

        var (suspendedResponse, suspendedRefusal) = await suspendedClient
            .GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());
        var (revokedResponse, revokedRefusal) = await revokedClient
            .GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        suspendedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden, "a suspended tenant is a refusal, never a sign-out");
        revokedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden, "so is a membership that was removed");

        suspendedRefusal.Errors.First().Code.Should().Be(ErrorCodes.TenantSuspended);
        revokedRefusal.Errors.First().Code.Should().Be(ErrorCodes.TenantMembershipRevoked);

        // The two explanations are different, which is the whole point: the caller who was removed from
        // a tenant and the caller whose tenant is out of service are told apart by what they are told.
        revokedRefusal.Errors.First().Reason.Should().NotBe(suspendedRefusal.Errors.First().Reason);

        var (suspendedInfoResponse, suspendedInfo) = await suspendedClient.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();
        var (revokedInfoResponse, revokedInfo) = await revokedClient.GETAsync<GetInfoEndpoint, UserGetInfoResponse>();

        suspendedInfoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        revokedInfoResponse.StatusCode.Should().Be(HttpStatusCode.OK);

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

        var storageProvider = App.Services.GetRequiredService<IStorageProvider>();
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
    /// Verifies that reactivating a tenant returns its members to work on the token that was refused,
    /// with no second sign-in - the other half of the refusal being a refusal of the operation rather
    /// than of the session (AC-008, AC-089).
    /// </summary>
    [Fact]
    public async Task Reactivation_Restores_Member_Access()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var member = await CreateTenantUserAsync(tenant.Id, roleId);
        var memberClient = await ClientForAsync(member.Username, tenant.Id);

        await SetPlatformAdminAuthTokenAsync();
        await SuspendTenantAsync(tenant.Id);

        var (refused, _) = await memberClient.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Reactivated by the platform surface, while the member's client goes on presenting the very
        // token it was refused with.
        await App.Client.POSTAsync<TenantReactivateEndpoint, TenantReactivateRequest, TenantReactivateResponse>(
            new() { Id = tenant.Id });

        var (restored, page) = await memberClient.GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(new());

        restored.StatusCode.Should().Be(HttpStatusCode.OK, "the member is admitted again on the token they never stopped presenting");
        page.Should().NotBeNull();
    }

    /// <summary>
    /// Suspends a tenant through the platform surface, so the operation is performed the one way it is
    /// performed in production and the tests are not asserting on a state they arranged themselves.
    /// </summary>
    /// <param name="tenantId">The tenant to put out of service.</param>
    private async Task SuspendTenantAsync(Guid tenantId)
    {
        var (response, _) = await App.Client
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
