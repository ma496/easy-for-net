using Efn.Features.Auth;
using Efn.Features.Auth.SignIn;
using Efn.Tests.Setup;

namespace Efn.Tests.Features.Auth.SignIn;

public class Tests(EndpointsFixture f, ITestOutputHelper o) : EndpointsTest(f, o)
{
    [Fact]
    public async Task SignIn_Success()
    {
        var (rsp, res) = await Fixture.Client.POSTAsync<Endpoint, Request, MyTokenResponse>(new()
        {
            Username = "admin",
            Password = "Admin@123"
        });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res?.AccessToken.Should().NotBeNullOrEmpty();
        res?.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SignIn_Fail_With_InvalidUsername()
    {
        var (rsp, res) = await Fixture.Client.POSTAsync<Endpoint, Request, ErrorResponse>(new()
        {
            Username = "423kdsjfkdjf324",
            Password = "Admin@123"
        });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Should().NotBeNull();
        res.Errors.Count.Should().Be(1);
        res.Errors.ContainsKey("generalErrors").Should().BeTrue();
        res.Errors["generalErrors"].Count.Should().Be(1);
        res.Errors["generalErrors"][0].Should().Be("Invalid user credentials!");
    }

    [Fact]
    public async Task SignIn_Fail_With_InvalidPassword()
    {
        var (rsp, res) = await Fixture.Client.POSTAsync<Endpoint, Request, ErrorResponse>(new()
        {
            Username = "admin",
            Password = "kdfjsdkjf2342ks"
        });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Should().NotBeNull();
        res.Errors.Count.Should().Be(1);
        res.Errors.ContainsKey("generalErrors").Should().BeTrue();
        res.Errors["generalErrors"].Count.Should().Be(1);
        res.Errors["generalErrors"][0].Should().Be("Invalid user credentials!");
    }
}
