using Backend.Features.Localization.Endpoints.Localization;

namespace Backend.Tests.Features.Localization.Endpoints.Localization;

/// <summary>
/// Tests for <see cref="LanguagesDeleteEndpoint"/>: removing the stored language row so the
/// shipped defaults apply again, idempotency, and the permission it requires.
/// </summary>
public class LanguagesDeleteTests(App app) : LocalizationTestsBase(app)
{
    [Fact]
    public async Task Delete_With_No_Row_Is_Idempotent()
    {
        var roleId = await CreateRoleAsync(Allow.Localization_Update);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);

        var (response, _) = await client.DELETEAsync<LanguagesDeleteEndpoint, EmptyResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, "a scope with nothing of its own to remove still answers success");
    }

    [Fact]
    public async Task Missing_Permission_Is_Forbidden()
    {
        var roleId = await CreateRoleAsync(Allow.Localization_View);
        var member = await CreateUserAsync(roleId);
        var client = await ClientForAsync(member.Username);

        var (response, _) = await client.DELETEAsync<LanguagesDeleteEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unauthenticated_Caller_Is_Refused()
    {
        var (response, _) = await Client.DELETEAsync<LanguagesDeleteEndpoint, ProblemDetails>();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
