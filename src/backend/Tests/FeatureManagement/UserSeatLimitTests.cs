namespace Backend.Tests.FeatureManagement;

using Backend.Exceptions;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Pins the seat limit <c>Identity.MaxUserCount</c> sets: an account beyond it is refused on every
/// path that brings one into a tenant, a platform account takes no seat, a removed member frees one,
/// and the users screen is told the same numbers the limit is enforced against.
/// </summary>
/// <remarks>
/// Every limit is written for a tenant the test created itself, so no other test's tenant is affected.
/// </remarks>
public class UserSeatLimitTests(App app) : FeatureTestsBase(app)
{
    [Fact]
    public async Task Creating_An_Account_Beyond_The_Limit_Is_Refused()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View, Allow.User_Create);
        var administrator = await CreateTenantUserAsync(tenant.Id, roleId);
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_MaxUserCount, "2");
        await SignInAsAsync(administrator.Username, tenant.Id);

        var (lastSeat, _) = await CreateUserAsync(roleId);
        lastSeat.StatusCode.Should().Be(HttpStatusCode.OK, "the administrator holds one seat and the plan allows two");

        var request = NewUserRequest(roleId);
        var (refused, problem) = await Client
            .POSTAsync<UserCreateEndpoint, UserCreateRequest, ProblemDetails>(request);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        problem.Errors.Should().ContainSingle()
               .Which.Code.Should().Be(ErrorCodes.FeatureLimitExceeded);
        (await DbContext.Users.AnyAsync(account => account.Username == request.Username, TestContext.Current.CancellationToken))
            .Should().BeFalse("a refused account is not left behind without its membership");
    }

    [Fact]
    public async Task Adding_A_Member_Beyond_The_Limit_Is_Refused_Even_From_The_Platform()
    {
        var tenant = await CreateTenantAsync();
        await CreateTenantUserAsync(tenant.Id);
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_MaxUserCount, "1");
        var newcomer = await CreateAccountWithoutMembershipAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, problem) = await AddMemberAsync(tenant.Id, newcomer.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the limit is a fact about the tenant, so it binds a platform administrator too");
        problem.Errors.Should().ContainSingle()
               .Which.Code.Should().Be(ErrorCodes.FeatureLimitExceeded);
        (await MembershipService.IsMemberAsync(tenant.Id, newcomer.Id, TestContext.Current.CancellationToken))
            .Should().BeFalse();
    }

    [Fact]
    public async Task A_Platform_Account_Takes_No_Seat()
    {
        var tenant = await CreateTenantAsync();
        await CreateTenantUserAsync(tenant.Id);
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_MaxUserCount, "1");
        var platformAccount = await CreateAccountWithoutMembershipAsync();
        await MarkAsPlatformAccountAsync(platformAccount.Id);
        await SetPlatformAdminAuthTokenAsync();

        var (response, _) = await AddMemberAsync(tenant.Id, platformAccount.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "every seat is taken, but a platform account needs none");
        (await TenantAuthorizationService.CountTenantSeatsAsync(tenant.Id, TestContext.Current.CancellationToken))
            .Should().Be(1);
    }

    [Fact]
    public async Task Removing_A_Member_Frees_A_Seat()
    {
        var tenant = await CreateTenantAsync();
        // Joined through the service so it becomes the tenant's administrator, which is what lets the
        // other member leave without tripping the last-administrator guard.
        var administrator = await CreateAccountWithoutMembershipAsync();
        await MembershipService.AddAsync(tenant.Id, administrator.Id, [], TestContext.Current.CancellationToken);
        var leaving = await CreateAccountWithoutMembershipAsync();
        await MembershipService.AddAsync(tenant.Id, leaving.Id, [], TestContext.Current.CancellationToken);
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_MaxUserCount, "2");
        var newcomer = await CreateAccountWithoutMembershipAsync();

        var full = async () => await MembershipService.AddAsync(tenant.Id, newcomer.Id, [], TestContext.Current.CancellationToken);
        (await full.Should().ThrowAsync<FeatureLimitExceededException>())
            .Which.Limit.Should().Be(2);

        (await MembershipService.RemoveAsync(tenant.Id, leaving.Id, TestContext.Current.CancellationToken))
            .Should().Be(TenantMembershipChangeOutcome.Applied);

        await MembershipService.AddAsync(tenant.Id, newcomer.Id, [], TestContext.Current.CancellationToken);
        (await MembershipService.IsMemberAsync(tenant.Id, newcomer.Id, TestContext.Current.CancellationToken))
            .Should().BeTrue();
    }

    [Fact]
    public async Task The_Seats_Endpoint_Reports_Usage_And_The_Limit()
    {
        var tenant = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(tenant.Id, Allow.User_View);
        var viewer = await CreateTenantUserAsync(tenant.Id, roleId);
        await CreateTenantUserAsync(tenant.Id);
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_MaxUserCount, "5");
        await SignInAsAsync(viewer.Username, tenant.Id);

        var (response, seats) = await Client.GETAsync<UserSeatsEndpoint, UserSeatsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        seats.Used.Should().Be(2);
        seats.Limit.Should().Be(5);
    }

    [Fact]
    public async Task The_Seats_Endpoint_Reports_No_Limit_In_Platform_Scope()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, seats) = await Client.GETAsync<UserSeatsEndpoint, UserSeatsResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        seats.Limit.Should().BeNull("platform scope is inside nobody's plan");
        seats.Used.Should().BePositive("the platform administrator itself is a platform account");
    }

    [Fact]
    public async Task The_Tenant_Seats_Endpoint_Reports_The_Route_Tenant_To_The_Platform()
    {
        var tenant = await CreateTenantAsync();
        await CreateTenantUserAsync(tenant.Id);
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_MaxUserCount, "1");
        await SetPlatformAdminAuthTokenAsync();

        var (response, seats) = await Client
            .GETAsync<TenantMemberSeatsEndpoint, TenantMemberSeatsRequest, TenantMemberSeatsResponse>(new() { TenantId = tenant.Id });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        seats.Used.Should().Be(1);
        seats.Limit.Should().Be(1, "the limit is the route tenant's, though the platform acts in no tenant");
    }

    [Fact]
    public async Task The_Tenant_Seats_Endpoint_Refuses_A_Caller_With_No_Standing_There()
    {
        var own = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        var roleId = await CreateTenantRoleAsync(own.Id, Allow.TenantMember_View);
        var viewer = await CreateTenantUserAsync(own.Id, roleId);
        await SignInAsAsync(viewer.Username, own.Id);

        var (response, problem) = await Client
            .GETAsync<TenantMemberSeatsEndpoint, TenantMemberSeatsRequest, ProblemDetails>(new() { TenantId = other.Id });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.Errors.Should().ContainSingle().Which.Code.Should().Be(ErrorCodes.NotTenantMember);
    }

    private Task<TestResult<ProblemDetails>> AddMemberAsync(Guid tenantId, Guid userId)
        => Client.POSTAsync<TenantMemberAddEndpoint, TenantMemberAddRequest, ProblemDetails>(new()
        {
            TenantId = tenantId,
            UserId = userId,
            Roles = []
        });

    private Task<TestResult<UserCreateResponse>> CreateUserAsync(Guid roleId)
        => Client.POSTAsync<UserCreateEndpoint, UserCreateRequest, UserCreateResponse>(NewUserRequest(roleId));

    private static UserCreateRequest NewUserRequest(Guid roleId)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        return new UserCreateRequest
        {
            Username = $"seat{suffix}",
            Email = $"seat{suffix}@example.com",
            Password = "Password#123",
            IsActive = true,
            Roles = [roleId]
        };
    }
}
