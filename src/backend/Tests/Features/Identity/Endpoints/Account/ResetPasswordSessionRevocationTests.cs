namespace Backend.Tests.Features.Identity.Endpoints.Account;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;

/// <summary>
/// Tests that completing a password reset ends every session of the account, and that a reset which does
/// not complete ends none.
/// </summary>
public class ResetPasswordSessionRevocationTests(App app) : SessionRevocationTestsBase(app)
{
    /// <summary>
    /// Verifies a completed reset ends every session of the account - whoever held the old password is
    /// locked out - while another account's session keeps working.
    /// </summary>
    [Fact]
    public async Task Resetting_A_Password_Ends_All_Its_Sessions()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        var first = await SessionForAsync(account.Username, tenant.Id);
        var second = await SessionForAsync(account.Username, tenant.Id);
        var bystander = await SessionForAsync((await CreateTenantUserAsync(tenant.Id)).Username, tenant.Id);
        var token = await Service<ITokenService>().GenerateTokenAsync(account.Id, TokenPurpose.PasswordReset);

        var response = await Client.POSTAsync<ResetPasswordEndpoint, ResetPasswordRequest>(
            new() { Token = token.Value, Password = "Reset#12345" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await AssertEndedAsync(first);
        await AssertEndedAsync(second);
        await AssertAliveAsync(bystander);
    }

    /// <summary>
    /// Verifies a reset refused for an unknown token ends no session.
    /// </summary>
    [Fact]
    public async Task A_Refused_Reset_Leaves_Sessions_Working()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id);
        var session = await SessionForAsync(account.Username, tenant.Id);

        var response = await Client.POSTAsync<ResetPasswordEndpoint, ResetPasswordRequest>(
            new() { Token = Guid.NewGuid().ToString("N"), Password = "Reset#12345" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertAliveAsync(session);
    }
}
