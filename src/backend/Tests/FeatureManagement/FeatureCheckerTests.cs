namespace Backend.Tests.FeatureManagement;

using Backend.Exceptions;

/// <summary>
/// Pins the ambient checker: it answers for the tenant the scope established, converts typed values,
/// and refuses loudly when it is asked with no scope at all.
/// </summary>
public class FeatureCheckerTests(App app) : FeatureTestsBase(app)
{
    private IFeatureChecker Checker => Service<IFeatureChecker>();

    [Fact]
    public async Task It_Answers_For_The_Tenant_The_Scope_Established()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");

        using var scope = TenantContext.BeginTenant(tenant.Id);

        (await Checker.IsEnabledAsync(FeatureNames.FileManagement_Enabled, TestContext.Current.CancellationToken))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Another_Tenants_Value_Does_Not_Reach_It()
    {
        var disabled = await CreateTenantAsync();
        var untouched = await CreateTenantAsync();
        await SetForTenantAsync(disabled.Id, FeatureNames.FileManagement_Enabled, "false");

        using var scope = TenantContext.BeginTenant(untouched.Id);

        (await Checker.IsEnabledAsync(FeatureNames.FileManagement_Enabled, TestContext.Current.CancellationToken))
            .Should().BeTrue();
    }

    [Fact]
    public async Task A_Typed_Value_Is_Converted()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_MaxUserCount, "250");

        using var scope = TenantContext.BeginTenant(tenant.Id);

        (await Checker.GetAsync(FeatureNames.Identity_MaxUserCount, 0, TestContext.Current.CancellationToken))
            .Should().Be(250);
    }

    [Fact]
    public async Task A_Value_That_Will_Not_Convert_Falls_Back_Rather_Than_Failing_The_Request()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.Identity_MaxUserCount, "not a number");

        using var scope = TenantContext.BeginTenant(tenant.Id);

        (await Checker.GetAsync(FeatureNames.Identity_MaxUserCount, 7, TestContext.Current.CancellationToken))
            .Should().Be(7);
    }

    [Fact]
    public async Task Checking_An_Enabled_Feature_Passes_Quietly()
    {
        var tenant = await CreateTenantAsync();

        using var scope = TenantContext.BeginTenant(tenant.Id);

        var act = async () => await Checker.CheckEnabledAsync(FeatureNames.FileManagement_Enabled,
                                                              TestContext.Current.CancellationToken);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Checking_A_Disabled_Feature_Names_It_In_The_Refusal()
    {
        var tenant = await CreateTenantAsync();
        await SetForTenantAsync(tenant.Id, FeatureNames.FileManagement_Enabled, "false");

        using var scope = TenantContext.BeginTenant(tenant.Id);

        var act = async () => await Checker.CheckEnabledAsync(FeatureNames.FileManagement_Enabled,
                                                              TestContext.Current.CancellationToken);
        (await act.Should().ThrowAsync<FeatureDisabledException>())
            .Which.FeatureName.Should().Be(FeatureNames.FileManagement_Enabled);
    }

    [Fact]
    public async Task Asking_With_No_Scope_Established_Is_Refused_Rather_Than_Answered_For_The_Platform()
    {
        using var unscoped = TenantContext.BeginUnscoped();

        var act = async () => await Checker.IsEnabledAsync(FeatureNames.FileManagement_Enabled,
                                                            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<TenantScopeNotEstablishedException>(
            "work that runs outside a request must name the tenant it acts for");
    }
}
