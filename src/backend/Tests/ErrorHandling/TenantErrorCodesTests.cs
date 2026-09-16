namespace Backend.Tests.ErrorHandling;

using System.Reflection;

/// <summary>
/// Tests that the error-code catalogue declares a distinct code for each tenant failure, under the
/// stable value the client and the translations are keyed on (AC-067).
/// </summary>
/// <remarks>
/// <para>
/// The values are asserted rather than the constants alone, because the value is the contract: a caller
/// reads the code out of a problem-details response, the web client resolves it through
/// <c>error.server.&lt;code&gt;</c>, and neither of them knows what the constant is called. A rename
/// that kept the value would therefore be invisible here (and harmless), while a change to the value
/// would break every caller that acts on the code - which is exactly what this test refuses.
/// </para>
/// <para>
/// The codes are also required to be distinct from one another. "Unknown tenant" and "not a member of
/// that tenant" are separate answers for the same surface, and a single code covering both would leave
/// the client unable to tell a caller which of the two happened - which is the whole reason the
/// catalogue exists.
/// </para>
/// </remarks>
public class TenantErrorCodesTests
{
    /// <summary>
    /// Every tenant failure and the code declared for it: the eleven the tenant feature answers with,
    /// plus the platform permission that cannot be granted inside a tenant and the concurrency refusal
    /// a membership change answers with.
    /// </summary>
    private static readonly Dictionary<string, string> TenantFailures = new()
    {
        [nameof(ErrorCodes.TenantNotFound)] = "tenantNotFound",
        [nameof(ErrorCodes.TenantSuspended)] = "tenantSuspended",
        [nameof(ErrorCodes.NotTenantMember)] = "notTenantMember",
        [nameof(ErrorCodes.NoActiveTenant)] = "noActiveTenant",
        [nameof(ErrorCodes.TenantIdentifierAlreadyExists)] = "tenantIdentifierAlreadyExists",
        [nameof(ErrorCodes.DuplicateTenantMembership)] = "duplicateTenantMembership",
        [nameof(ErrorCodes.LastTenantAdministrator)] = "lastTenantAdministrator",
        [nameof(ErrorCodes.SystemCreatedTenantCannotBeModified)] = "systemCreatedTenantCannotBeModified",
        [nameof(ErrorCodes.CrossTenantFileAccess)] = "crossTenantFileAccess",
        [nameof(ErrorCodes.TenantMembershipRevoked)] = "tenantMembershipRevoked",
        [nameof(ErrorCodes.PlatformPermissionNotGrantable)] = "platformPermissionNotGrantable",
        [nameof(ErrorCodes.ConcurrentModification)] = "concurrentModification"
    };

    /// <summary>
    /// Verifies that every tenant failure has its own declared code, carrying the value the client and
    /// the translations are keyed on, and that no two failures share one (AC-067).
    /// </summary>
    [Fact]
    public void All_Tenant_Error_Codes_Are_Declared()
    {
        var declared = DeclaredCodes();

        foreach (var (constant, code) in TenantFailures)
        {
            declared.Should().ContainKey(constant,
                "{0} is the code the refusal for this failure is answered with, and the one every caller resolving it reads",
                code);

            declared[constant].Should().Be(code,
                "the value is the contract: the constant's name is not what a caller receives or acts on");
        }

        foreach (var code in TenantFailures.Values)
        {
            declared.Values.Count(value => value == code).Should().Be(1,
                "{0} names exactly one failure, so that a caller reading it is told which one happened",
                code);
        }
    }

    /// <summary>
    /// Every code the catalogue declares, by the constant that declares it, read by reflection so that
    /// the check is against what the assembly holds rather than against a copy of it.
    /// </summary>
    /// <returns>The declared code values, keyed by constant name.</returns>
    private static Dictionary<string, string> DeclaredCodes()
        => typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .ToDictionary(
                field => field.Name,
                field => (string)field.GetRawConstantValue()!);
}
