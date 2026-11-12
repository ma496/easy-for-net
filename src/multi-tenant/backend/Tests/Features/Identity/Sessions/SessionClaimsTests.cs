using System.Security.Claims;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Sessions;

namespace Backend.Tests.Features.Identity.Sessions;

/// <summary>
/// Tests that the projection of a stored session onto a principal is the only source of the claims it
/// writes, whatever the credential arrived carrying.
/// </summary>
public class SessionClaimsTests
{
    /// <summary>
    /// Verifies claims of the projected types that arrived on the credential are removed, and only the
    /// session's are left; the account and session identifiers are untouched.
    /// </summary>
    [Fact]
    public void Project_Replaces_Claims_That_Arrived_With_The_Credential()
    {
        var userId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimConstants.SessionId, "abc"),
            new Claim(ClaimTypes.Role, "Smuggled"),
            new Claim(ClaimConstants.Permission, "smuggled.permission"),
            new Claim(ClaimConstants.TenantId, Guid.NewGuid().ToString()),
            new Claim(ClaimConstants.TenantIdentifier, "smuggled-tenant"),
            new Claim(ClaimConstants.IsPlatform, bool.TrueString),
            new Claim(ClaimTypes.Name, "smuggled"),
            new Claim(ClaimTypes.Email, "smuggled@example.com"),
        ], "test");

        SessionClaims.Project(identity, new SessionRecord
        {
            SessionId = "abc",
            UserId = userId,
            Username = "real",
            Email = "real@example.com",
            IsPlatform = false,
            TenantId = null,
            Roles = ["Real"],
            Permissions = ["real.permission"],
        });

        identity.FindAll(ClaimTypes.Role).Select(c => c.Value).Should().Equal("Real");
        identity.FindAll(ClaimConstants.Permission).Select(c => c.Value).Should().Equal("real.permission");
        identity.FindAll(ClaimTypes.Name).Select(c => c.Value).Should().Equal("real");
        identity.FindAll(ClaimTypes.Email).Select(c => c.Value).Should().Equal("real@example.com");
        identity.FindAll(ClaimConstants.TenantId).Should().BeEmpty("the session acts in no tenant, whatever the credential said");
        identity.FindAll(ClaimConstants.TenantIdentifier).Should().BeEmpty("nor does it carry a tenant identifier");
        identity.FindAll(ClaimConstants.IsPlatform).Should().BeEmpty("the session is not a platform one, whatever the credential said");
        identity.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(userId.ToString());
        identity.FindFirst(ClaimConstants.SessionId)!.Value.Should().Be("abc");
    }

    /// <summary>
    /// Verifies a session acting in a tenant projects that tenant's id and its identifier, which is what a tenant
    /// host is compared against.
    /// </summary>
    [Fact]
    public void Project_Writes_The_Tenant_Identifier_With_The_Tenant()
    {
        var tenantId = Guid.NewGuid();
        var identity = new ClaimsIdentity([new Claim(ClaimConstants.TenantIdentifier, "smuggled-tenant")], "test");

        SessionClaims.Project(identity, new SessionRecord
        {
            SessionId = "abc",
            UserId = Guid.NewGuid(),
            Username = "real",
            Email = "real@example.com",
            TenantId = tenantId,
            TenantIdentifier = "acme",
        });

        identity.FindAll(ClaimConstants.TenantId).Select(c => c.Value).Should().Equal(tenantId.ToString());
        identity.FindAll(ClaimConstants.TenantIdentifier).Select(c => c.Value).Should().Equal("acme");
    }
}
