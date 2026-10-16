using Backend.Features.Tenancy.Core.Hosting;
using Microsoft.AspNetCore.Http;

namespace Backend.Tests.Features.Tenancy.Core.Hosting;

/// <summary>
/// Unit tests for <see cref="TenantHostMap"/>: the template's validation and the parsing of a request host
/// into the platform, a tenant or nothing - ports, case, nested labels, look-alike hosts, the platform's own
/// label, and resolution turned off.
/// </summary>
public class TenantHostMapTests
{
    private const string DevelopmentPlatform = "http://app.localhost:3000";
    private const string DevelopmentTemplate = "http://{tenant}.localhost:3000";

    private static readonly TenantHostMap _development = new(DevelopmentPlatform, DevelopmentTemplate);
    private static readonly TenantHostMap _production = new("https://app.example.com", "https://{tenant}.example.com");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://{tenant}.localhost:3000")]
    [InlineData("https://{tenant}.example.com")]
    [InlineData("https://{tenant}.example.com/")]
    [InlineData("https://{tenant}.apps.example.com:8443")]
    public void Valid_Templates_Are_Accepted(string? template)
        => TenantHostMap.IsValidTemplate(template).Should().BeTrue();

    [Theory]
    [InlineData("https://example.com")]                          // no placeholder
    [InlineData("https://{tenant}.{tenant}.example.com")]         // two placeholders
    [InlineData("https://app-{tenant}.example.com")]              // not the entire first label
    [InlineData("https://{tenant}-app.example.com")]              // not the entire first label
    [InlineData("https://app.{tenant}.example.com")]              // not the first label
    [InlineData("https://{tenant}")]                              // no suffix at all
    [InlineData("{tenant}.example.com")]                          // not an absolute URI
    [InlineData("ftp://{tenant}.example.com")]                    // not http(s)
    [InlineData("https://{tenant}.example.com/path")]             // more than an origin
    [InlineData("https://{tenant}.example.com?x=1")]              // more than an origin
    [InlineData("https://{tenant}.exa mple.com")]                 // does not parse
    public void Invalid_Templates_Are_Refused(string template)
        => TenantHostMap.IsValidTemplate(template).Should().BeFalse();

    [Theory]
    [InlineData("acme.localhost:3000", "acme")]
    [InlineData("ACME.LocalHost:3000", "acme")]
    [InlineData("t-1234.localhost:3000", "t-1234")]
    public void Tenant_Host_Resolves_To_Its_Lower_Case_Label(string host, string identifier)
        => _development.Resolve(new HostString(host)).Should().Be(TenantHost.ForTenant(identifier));

    [Theory]
    [InlineData("app.localhost:3000")]
    [InlineData("APP.localhost:3000")]
    public void Platform_Host_Resolves_To_Platform_Not_A_Tenant_Named_App(string host)
        => _development.Resolve(new HostString(host)).Should().Be(TenantHost.Platform);

    [Theory]
    [InlineData("a.b.localhost:3000")]           // nested labels
    [InlineData("acmelocalhost:3000")]           // look-alike without the dot
    [InlineData("acme.localhost:3001")]          // another port
    [InlineData("acme.localhost")]               // no port, and 3000 is not http's default
    [InlineData("acme.localhost.evil.com:3000")] // the suffix inside another domain
    [InlineData("localhost:3000")]               // the bare suffix
    [InlineData(".localhost:3000")]              // an empty label
    [InlineData("ac_me.localhost:3000")]         // not an identifier's characters
    [InlineData("app.localhost:3001")]           // the platform host on another port
    [InlineData("example.org")]
    public void Other_Hosts_Resolve_To_None(string host)
        => _development.Resolve(new HostString(host)).Should().Be(TenantHost.None);

    [Theory]
    [InlineData("acme.example.com", TenantHostKind.Tenant)]       // no port: the template's default port
    [InlineData("acme.example.com:443", TenantHostKind.Tenant)]   // the default port, explicit
    [InlineData("acme.example.com:8443", TenantHostKind.None)]
    [InlineData("acme.example.com:80", TenantHostKind.None)]
    [InlineData("app.example.com", TenantHostKind.Platform)]
    [InlineData("app.example.com:443", TenantHostKind.Platform)]
    [InlineData("app.example.com:80", TenantHostKind.None)]
    public void Default_Ports_Are_Implied(string host, TenantHostKind kind)
        => _production.Resolve(new HostString(host)).Kind.Should().Be(kind);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void An_Empty_Template_Turns_Resolution_Off(string? template)
    {
        var map = new TenantHostMap(DevelopmentPlatform, template);

        map.IsEnabled.Should().BeFalse();
        map.Resolve(new HostString("acme.localhost:3000")).Should().Be(TenantHost.None);
        map.Resolve(new HostString("app.localhost:3000")).Should().Be(TenantHost.None, "the platform host names nothing either while resolution is off");
        map.TenantUrl("acme").Should().BeNull();
        map.TenantUrlTemplate().Should().BeNull();
        map.PlatformUrl().Should().Be(DevelopmentPlatform);
    }

    [Fact]
    public void Urls_Are_Built_From_The_Configuration()
    {
        _development.TenantUrl(" Acme ").Should().Be("http://acme.localhost:3000");
        _development.PlatformUrl().Should().Be(DevelopmentPlatform);
        _production.TenantUrl("acme").Should().Be("https://acme.example.com");
        _development.TenantUrlTemplate().Should().Be("http://{tenant}.localhost:3000");
        _production.TenantUrlTemplate().Should().Be("https://{tenant}.example.com");
    }

    [Fact]
    public void An_Absent_Host_Resolves_To_None()
        => _development.Resolve(default).Should().Be(TenantHost.None);
}
