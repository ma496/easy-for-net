namespace Efn.Tests.Setup;

[Collection("EndpointsFixtureCollection")]
public abstract class EndpointsTest
{
    protected EndpointsTest(EndpointsFixture fixture, ITestOutputHelper output)
    {
        Fixture = fixture;
        Output = output;
    }

    public EndpointsFixture Fixture { get; }
    public ITestOutputHelper Output { get; set; }
}
