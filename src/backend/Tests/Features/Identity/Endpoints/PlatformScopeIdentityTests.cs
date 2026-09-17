namespace Backend.Tests.Features.Identity.Endpoints;

using Backend.Features.Identity.Endpoints.Roles;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Features.Notifications.Core;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Notifications.Endpoints.Notifications;
using Backend.Tests.Features.Tenancy;

/// <summary>
/// Tests for the user, role and notification endpoints called by a platform administrator acting in no
/// tenant, where the request runs in platform scope: what is read is the platform's own - platform roles,
/// the accounts holding them, notifications belonging to no tenant - and what is created belongs to no
/// tenant.
/// </summary>
/// <remarks>
/// Every test signs in as the seeded platform administrator, which holds no membership, so its session
/// never carries a tenant. Names are derived from fresh identifiers because platform roles share one
/// uniqueness scope across the whole suite.
/// </remarks>
public class PlatformScopeIdentityTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a role created with no tenant active is a platform role.
    /// </summary>
    [Fact]
    public async Task Creates_A_Platform_Role()
    {
        await SetPlatformAdminAuthTokenAsync();

        var roleId = await CreatePlatformRoleAsync();

        var stored = await DbContext.Roles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(role => role.Id == roleId, TestContext.Current.CancellationToken);

        stored.TenantId.Should().BeNull("platform scope attributes the new role to no tenant");
        stored.SystemCreated.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that the role list shows the platform roles alone, and still answers for a tenant that is
    /// named, which is what the tenant member screens pick roles from.
    /// </summary>
    [Fact]
    public async Task Lists_Platform_Roles_Unless_A_Tenant_Is_Named()
    {
        var tenant = await CreateTenantAsync();
        var tenantRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);

        await SetPlatformAdminAuthTokenAsync();
        var platformRoleId = await CreatePlatformRoleAsync();

        var (response, page) = await App.Client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { All = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var ids = page.Items.Select(item => item.Id).ToList();
        ids.Should().Contain(platformRoleId).And.Contain(TestRoles.PlatformAdminRoleId);
        ids.Should().NotContain(tenantRoleId, "a tenant's role is not one of the platform's roles");

        var (namedResponse, named) = await App.Client
            .GETAsync<RoleListEndpoint, RoleListRequest, RoleListResponse>(new() { All = true, TenantId = tenant.Id });

        namedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        named.Items.Select(item => item.Id).Should().Contain(tenantRoleId).And.NotContain(platformRoleId);
    }

    /// <summary>
    /// Verifies that a tenant's role is out of reach by identifier from platform scope, answered as a
    /// role that does not exist.
    /// </summary>
    [Fact]
    public async Task Does_Not_Read_A_Tenant_Role()
    {
        var tenant = await CreateTenantAsync();
        var tenantRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);

        await SetPlatformAdminAuthTokenAsync();

        var (response, _) = await App.Client
            .GETAsync<RoleGetEndpoint, RoleGetRequest, RoleGetResponse>(new() { Id = tenantRoleId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that the user list shows the accounts holding a platform role and leaves out accounts
    /// that only belong to a tenant.
    /// </summary>
    [Fact]
    public async Task Lists_Only_Platform_Users()
    {
        var tenant = await CreateTenantAsync();
        var tenantUser = await CreateTenantUserAsync(tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.Role_View));

        await SetPlatformAdminAuthTokenAsync();
        var platformRoleId = await CreatePlatformRoleAsync();

        var (created, platformUser) = await App.Client
            .POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(NewUserRequest(platformRoleId));

        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var (platformRsp, platformPage) = await App.Client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
                new() { Page = 1, PageSize = 10, Search = platformUser.Username });

        platformRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        platformPage.Items.Select(item => item.Id).Should().Equal([platformUser.Id]);

        var (tenantRsp, tenantPage) = await App.Client
            .GETAsync<UserListEndpoint, UserListRequest, UserListResponse>(
                new() { Page = 1, PageSize = 10, Search = tenantUser.Username });

        tenantRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        tenantPage.Total.Should().Be(0, "an account holding no platform role is not one of the platform's users");
    }

    /// <summary>
    /// Verifies that an account created with no tenant active joins no tenant, holds the platform role it
    /// was given, and cannot be given a tenant's role from there.
    /// </summary>
    [Fact]
    public async Task Creates_An_Account_With_No_Membership_Holding_Platform_Roles()
    {
        var tenant = await CreateTenantAsync();
        var tenantRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);

        await SetPlatformAdminAuthTokenAsync();
        var platformRoleId = await CreatePlatformRoleAsync();

        var (refused, refusal) = await App.Client
            .POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(NewUserRequest(tenantRoleId));

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest, "only platform roles can be granted from platform scope");
        refusal.Errors.First().Code.Should().Be(ErrorCodes.ReferencedRecordNotFound);

        var (created, account) = await App.Client
            .POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(NewUserRequest(platformRoleId));

        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var memberships = await DbContext.TenantMemberships
            .AcrossAllTenants()
            .AsNoTracking()
            .CountAsync(membership => membership.UserId == account.Id, TestContext.Current.CancellationToken);

        memberships.Should().Be(0, "an account created in platform scope joins no tenant");

        var heldRoleIds = await DbContext.UserRoles
            .AsNoTracking()
            .Where(assignment => assignment.UserId == account.Id)
            .Select(assignment => assignment.RoleId)
            .ToListAsync(TestContext.Current.CancellationToken);

        heldRoleIds.Should().Equal([platformRoleId]);
    }

    /// <summary>
    /// Verifies that updating a platform user's roles from platform scope replaces its platform roles and
    /// leaves the roles it holds inside a tenant alone.
    /// </summary>
    [Fact]
    public async Task Updating_Roles_Leaves_Tenant_Roles_Alone()
    {
        var tenant = await CreateTenantAsync();
        var tenantRoleId = await CreateTenantRoleAsync(tenant.Id, Allow.Role_View);
        var member = await CreateTenantUserAsync(tenant.Id, tenantRoleId);

        await SetPlatformAdminAuthTokenAsync();
        var firstPlatformRoleId = await CreatePlatformRoleAsync();
        var secondPlatformRoleId = await CreatePlatformRoleAsync();

        // Holding a platform role is what makes the member one of the platform's users at all.
        await UserService.AssignRoleAsync(member.Id, firstPlatformRoleId);

        var (response, _) = await App.Client
            .PUTAsync<UserUpdateEndpoint, UserUpdateRequest, UserUpdateResponse>(new()
            {
                Id = member.Id,
                IsActive = true,
                Roles = [secondPlatformRoleId]
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var heldRoleIds = await DbContext.UserRoles
            .AsNoTracking()
            .Where(assignment => assignment.UserId == member.Id)
            .Select(assignment => assignment.RoleId)
            .ToListAsync(TestContext.Current.CancellationToken);

        heldRoleIds.Should().BeEquivalentTo(
            [tenantRoleId, secondPlatformRoleId],
            "platform scope replaces the account's platform roles, never what it holds in a tenant");
    }

    /// <summary>
    /// Verifies that the notification list shows the notifications belonging to no tenant and leaves out
    /// one raised inside a tenant, even when it is addressed to the same account.
    /// </summary>
    [Fact]
    public async Task Lists_Only_Platform_Notifications()
    {
        var tenant = await CreateTenantAsync();
        var group = $"platform-{Guid.NewGuid():N}";

        Guid platformNotificationId;
        using (TenantContext.BeginPlatformScope())
        {
            var notification = NewNotification(group);
            DbContext.Notifications.Add(notification);
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            platformNotificationId = notification.Id;
        }

        using (TenantContext.BeginTenant(tenant.Id))
        {
            DbContext.Notifications.Add(NewNotification(group));
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await SetPlatformAdminAuthTokenAsync();

        var (response, page) = await App.Client
            .GETAsync<NotificationListEndpoint, NotificationListRequest, NotificationListResponse>(
                new() { Page = 1, PageSize = 10, Group = group });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Items.Select(item => item.Id).Should().Equal(
            [platformNotificationId],
            "a notification raised inside a tenant belongs to that tenant, not to the platform");
    }

    /// <summary>
    /// Creates a platform role through the endpoint, as the signed-in platform administrator.
    /// </summary>
    /// <returns>The identifier of the created role.</returns>
    private async Task<Guid> CreatePlatformRoleAsync()
    {
        var (response, role) = await App.Client
            .POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(
                new() { Name = $"Platform {Guid.NewGuid():N}" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return role.Id;
    }

    /// <summary>
    /// Builds a create-user request with unique sign-in identifiers and the one role named.
    /// </summary>
    /// <param name="roleId">The role the account is to start with.</param>
    /// <returns>The request.</returns>
    private static UserCreateRequest NewUserRequest(Guid roleId)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];

        return new()
        {
            Username = $"p{suffix}",
            Email = $"p{suffix}@example.com",
            Password = TestUsers.DefaultPassword,
            IsActive = true,
            Roles = [roleId]
        };
    }

    /// <summary>
    /// Builds an unread notification addressed to the seeded platform administrator, in the group named
    /// so the test can list its own rows alone.
    /// </summary>
    /// <param name="group">The group the notification is filed under.</param>
    /// <returns>The notification.</returns>
    private static Notification NewNotification(string group)
        => new()
        {
            UserId = TestUsers.PlatformAdminUserId,
            Type = NotificationType.Info,
            TitleKey = "notifications.systemUpdate.title",
            MessageKey = "notifications.systemUpdate.message",
            Group = group
        };
}
