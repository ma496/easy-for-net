using Efn.Features.SignUp;
using Efn.Tests.Setup;

namespace Efn.Tests.Features.SignUp;

public class Tests(EndpointsFixture f, ITestOutputHelper o) : EndpointsTest(f, o)
{
    [Fact]
    public async Task Signup_Success()
    {
        var request = new RequestFaker().Generate();

        var (rsp, res) = await Fixture.Client.POSTAsync<Endpoint, Request, Response>(request);

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Should().NotBeNull();
        res.Message.Should().Contain($"The user [{request.Name}] has been created with ID:");
    }

    [Fact]
    public async Task Signup_Fail_With_EmptyData()
    {
        var request = new Request();

        var (rsp, res) = await Fixture.Client.POSTAsync<Endpoint, Request, ErrorResponse>(request);

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Should().NotBeNull();
        res.Errors.Count.Should().Be(4);
    }

    [Fact]
    public async Task Signup_Fail_With_InvalidEmail()
    {
        var request = new RequestFaker().Generate();
        request.Email = $"invalidemail";

        var (rsp, res) = await Fixture.Client.POSTAsync<Endpoint, Request, ErrorResponse>(request);

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Should().NotBeNull();
        res.Errors.Count.Should().Be(1);
        res.Errors["email"][0].Should().Be("'Email' is not a valid email address.");
    }
}
