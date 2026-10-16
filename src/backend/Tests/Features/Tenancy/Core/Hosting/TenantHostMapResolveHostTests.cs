using Backend.Features.Tenancy.Core.Hosting;

namespace Backend.Tests.Features.Tenancy.Core.Hosting;

/// <summary>
/// Tests for <see cref="TenantHostMap.ResolveHost"/> - a host handed in as text, read by the same rules as a
/// request's host - and <see cref="TenantHostMap.Origin"/>, the origin links point at for a resolution.
/// </summary>
public class TenantHostMapResolveHostTests
{
    private static readonly TenantHostMap _map = new("https://app.example.com", "https://{tenant}.example.com");

    [Fact]
    public void Resolves_The_Platform_And_Tenant_Hosts()
    {
        _map.ResolveHost("app.example.com").Should().Be(TenantHost.Platform);
        _map.ResolveHost("Acme.Example.com").Should().Be(TenantHost.ForTenant("acme"));
        _map.ResolveHost("acme.example.com:443").Should().Be(TenantHost.ForTenant("acme"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("a.b.example.com")]
    [InlineData("acme.example.com.evil.test")]
    [InlineData("acme.example.com:8443")]
    [InlineData("https://acme.example.com")]
    [InlineData("acme.example.com/path")]
    [InlineData("user@acme.example.com")]
    [InlineData(" acme.example.com")]
    public void Names_Nothing_For_Anything_Else(string? host)
        => _map.ResolveHost(host).Should().Be(TenantHost.None);

    [Fact]
    public void Names_Nothing_With_Resolution_Off()
        => new TenantHostMap("https://app.example.com", "").ResolveHost("app.example.com").Should().Be(TenantHost.None);

    [Fact]
    public void Origin_Is_The_Tenants_On_A_Tenant_Host_And_The_Platforms_Otherwise()
    {
        _map.Origin(TenantHost.ForTenant("acme")).Should().Be("https://acme.example.com");
        _map.Origin(TenantHost.Platform).Should().Be("https://app.example.com");
        _map.Origin(TenantHost.None).Should().Be("https://app.example.com");
        new TenantHostMap("https://app.example.com", "").Origin(TenantHost.ForTenant("acme")).Should().Be("https://app.example.com");
    }
}
