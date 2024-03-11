using Efn.Tests.Setup;
using Efn.Features.Auth.Login;
using System.Text;
using System.Text.Json;
using Efn.Features.Auth;
using FastEndpoints.Security;

namespace Efn.Tests.Features.Auth.RefreshToken;

public class Tests(EndpointsFixture f, ITestOutputHelper o) : EndpointsTest(f, o)
{
    [Fact]
    public async Task RefreshToken_Success()
    {
        var (rsp, res) = await Fixture.Client.POSTAsync<Endpoint, Request, MyTokenResponse>(new()
        {
            Username = "admin",
            Password = "Admin@123"
        });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);

        TokenRequest tokenRequest = res;
        var jsonContent = JsonSerializer.Serialize(tokenRequest);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        var refreshTokenResult = await Fixture.Client.PostAsync("api/auth/refresh-token", content);
        var refreshTokenRes = JsonSerializer.Deserialize<TokenResponse>(await refreshTokenResult.Content.ReadAsStringAsync(),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

        refreshTokenResult.StatusCode.Should().Be(HttpStatusCode.OK);
        refreshTokenRes?.AccessToken.Should().NotBeNullOrEmpty();
        refreshTokenRes?.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshToken_Fail_With_InvalidUserId()
    {
        var (rsp, res) = await Fixture.Client.POSTAsync<Endpoint, Request, MyTokenResponse>(new()
        {
            Username = "admin",
            Password = "Admin@123"
        });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);

        TokenRequest tokenRequest = res;
        tokenRequest.UserId = Guid.NewGuid().ToString();
        var jsonContent = JsonSerializer.Serialize(tokenRequest);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        var refreshTokenResult = await Fixture.Client.PostAsync("api/auth/refresh-token", content);
        var refreshTokenRes = JsonSerializer.Deserialize<ErrorResponse>(await refreshTokenResult.Content.ReadAsStringAsync(),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

        refreshTokenResult.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refreshTokenRes.Should().NotBeNull();
        refreshTokenRes?.Errors.Count.Should().Be(1);
        refreshTokenRes?.Errors.ContainsKey("generalErrors").Should().BeTrue();
        refreshTokenRes?.Errors["generalErrors"].Count.Should().Be(1);
        refreshTokenRes?.Errors["generalErrors"][0].Should().Be("The refresh token is not valid!");
    }

    [Fact]
    public async Task RefreshToken_Fail_With_InvalidRefreshToken()
    {
        var (rsp, res) = await Fixture.Client.POSTAsync<Endpoint, Request, MyTokenResponse>(new()
        {
            Username = "admin",
            Password = "Admin@123"
        });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);

        TokenRequest tokenRequest = res;
        tokenRequest.RefreshToken = Guid.NewGuid().ToString();
        var jsonContent = JsonSerializer.Serialize(tokenRequest);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        var refreshTokenResult = await Fixture.Client.PostAsync("api/auth/refresh-token", content);
        var refreshTokenRes = JsonSerializer.Deserialize<ErrorResponse>(await refreshTokenResult.Content.ReadAsStringAsync(),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

        refreshTokenResult.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refreshTokenRes.Should().NotBeNull();
        refreshTokenRes?.Errors.Count.Should().Be(1);
        refreshTokenRes?.Errors.ContainsKey("generalErrors").Should().BeTrue();
        refreshTokenRes?.Errors["generalErrors"].Count.Should().Be(1);
        refreshTokenRes?.Errors["generalErrors"][0].Should().Be("The refresh token is not valid!");
    }
}
