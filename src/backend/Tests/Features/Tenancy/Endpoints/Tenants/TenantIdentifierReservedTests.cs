using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Tenancy.Core;
using Backend.Features.Tenancy.Endpoints.Tenants;
using Backend.Settings;
using Microsoft.Extensions.Options;

namespace Backend.Tests.Features.Tenancy.Endpoints.Tenants;

/// <summary>
/// Tests that the identifiers which would collide with the hosts a deployment serves beside its tenants -
/// and the platform host's own first label - are refused with <c>tenantIdentifierReserved</c> by the one
/// shared rule, at sign-up, tenant creation and tenant update alike.
/// </summary>
public class TenantIdentifierReservedTests(App app) : TenancyTestsBase(app)
{
    /// <summary>The fixed list, and the platform host's first label when its host has more than one.</summary>
    [Fact]
    public void Reserved_Set_Adds_The_Platform_Hosts_First_Label()
    {
        TenantValidationRules.ReservedIdentifiers("https://portal.example.com").Should().Contain("portal")
            .And.Contain(TenantValidationRules.AlwaysReservedIdentifiers);
        TenantValidationRules.ReservedIdentifiers("http://app.localhost:3000").Should().BeEquivalentTo(TenantValidationRules.AlwaysReservedIdentifiers);
        TenantValidationRules.ReservedIdentifiers("http://localhost:3000").Should().BeEquivalentTo(TenantValidationRules.AlwaysReservedIdentifiers,
            "a single-label platform host reserves nothing more");
        TenantValidationRules.ReservedIdentifiers("https://portal.example.com").Contains("PORTAL").Should().BeTrue("the comparison ignores case");
    }

    /// <summary>Sign-up refuses a reserved identifier, the platform host's own first label included, and creates nothing.</summary>
    [Fact]
    public async Task Sign_Up_Refuses_A_Reserved_Identifier()
    {
        var platformLabel = PlatformLabel();
        foreach (var identifier in new[] { "admin", "hangfire", platformLabel }.OfType<string>())
        {
            var username = $"signup-{Guid.NewGuid():N}";
            using var visitor = App.CreateClient(new ClientOptions { HandleCookies = false });

            var (response, refusal) = await visitor.POSTAsync<SignupEndpoint, SignupRequest, ProblemDetails>(new()
            {
                Username = username,
                Email = $"{username}@example.com",
                Password = TestUsers.DefaultPassword,
                ConfirmPassword = TestUsers.DefaultPassword,
                TenantName = $"Tenant {Guid.NewGuid():N}",
                TenantIdentifier = identifier
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            refusal.Errors.Should().ContainSingle(error => error.Code == ErrorCodes.TenantIdentifierReserved.Value)
                .Which.Name.Should().Be("tenantIdentifier");
        }
    }

    /// <summary>
    /// Tenant creation refuses a reserved identifier, surrounding white-space trimmed. (An upper-case one breaks
    /// the shape rule first, and the response carries one error per field.)
    /// </summary>
    [Fact]
    public async Task Tenant_Create_Refuses_A_Reserved_Identifier()
    {
        await SetPlatformAdminAuthTokenAsync();

        foreach (var identifier in new[] { "www", " cdn " })
        {
            var (response, refusal) = await Client.POSTAsync<TenantCreateEndpoint, TenantCreateRequest, ProblemDetails>(new()
            {
                Name = $"Tenant {Guid.NewGuid():N}",
                Identifier = identifier
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            refusal.Errors.Select(error => error.Code).Should().Contain(ErrorCodes.TenantIdentifierReserved.Value);
        }

        (await TenantService.IdentifierExistsAsync("www", cancellationToken: TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    /// <summary>Renaming a tenant to a reserved identifier is refused and leaves it as it was.</summary>
    [Fact]
    public async Task Tenant_Update_Refuses_A_Reserved_Identifier()
    {
        var tenant = await CreateTenantAsync();
        await SetPlatformAdminAuthTokenAsync();

        var (response, refusal) = await Client.PUTAsync<TenantUpdateEndpoint, TenantUpdateRequest, ProblemDetails>(new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Identifier = "docs",
            EditionId = tenant.EditionId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        refusal.Errors.Should().ContainSingle(error => error.Code == ErrorCodes.TenantIdentifierReserved.Value)
            .Which.Name.Should().Be("identifier");

        var stored = await DbContext.Tenants.AsNoTracking().AcrossAllTenants()
            .Where(row => row.Id == tenant.Id)
            .Select(row => row.IdentifierNormalized)
            .SingleAsync(TestContext.Current.CancellationToken);
        stored.Should().Be(tenant.IdentifierNormalized);
    }

    /// <summary>The first label of this host's platform origin when it is reserved by being one, else null.</summary>
    private string? PlatformLabel()
    {
        var defaultDomain = Service<IOptions<WebSetting>>().Value.DefaultDomain;
        var extra = TenantValidationRules.ReservedIdentifiers(defaultDomain).Except(TenantValidationRules.AlwaysReservedIdentifiers).SingleOrDefault();
        return extra is { Length: >= TenantValidationRules.IdentifierMinLength } ? extra : null;
    }
}
