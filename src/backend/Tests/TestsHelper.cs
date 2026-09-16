namespace Backend.Tests;

using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Tenancy.Endpoints.Tenants;
using System.Net.Http.Headers;

public static class TestsHelper
{
    /// <summary>
    /// Signs in and returns the access token the caller was issued, optionally selecting a tenant
    /// first. Sign-in resolves an active tenant on its own only when exactly one membership stands,
    /// so a tenant is named here whenever the account holds several - or none - and the caller needs
    /// to act in a particular one.
    /// </summary>
    public static async Task<string> GetNewAuthTokenAsync(HttpClient client, string username = "admin", string password = "Admin#123", Guid? tenantId = null)
    {
        var (_, res) = await client.POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(
            new() { Username = username, Password = password });

        if (tenantId is not { } activeTenantId)
        {
            return res.AccessToken;
        }

        // Switching is an authenticated call, so the token just issued has to be presented to make
        // it. The client's own header is restored afterwards: resolving a token for an account must
        // not sign the client in as that account.
        var previous = client.DefaultRequestHeaders.Authorization;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", res.AccessToken);
        try
        {
            return await SwitchTenantAsync(client, activeTenantId);
        }
        finally
        {
            client.DefaultRequestHeaders.Authorization = previous;
        }
    }

    /// <summary>
    /// Signs in, optionally selects a tenant, and leaves the client presenting the resulting token.
    /// </summary>
    public static async Task SetNewAuthTokenAsync(HttpClient client, string username = "admin", string password = "Admin#123", Guid? tenantId = null)
        => SetAuthToken(client, await GetNewAuthTokenAsync(client, username, password, tenantId));

    /// <summary>
    /// Leaves the client presenting this token on every later request.
    /// </summary>
    public static void SetAuthToken(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public static void ClearAuthToken(HttpClient client)
        => client.DefaultRequestHeaders.Authorization = null;

    /// <summary>
    /// Re-establishes the signed-in caller's session in the tenant named and leaves the client
    /// presenting the token that session issued. The identity is unchanged - this is the same
    /// account acting in a different tenant, not a second sign-in - so the token it replaces is what
    /// authorises the switch.
    /// </summary>
    /// <returns>The access token of the tenant-scoped session.</returns>
    public static async Task<string> SwitchTenantAsync(HttpClient client, Guid tenantId)
    {
        var (_, res) = await client.POSTAsync<TenantSwitchEndpoint, TenantSwitchRequest, TenantSwitchResponse>(
            new() { TenantId = tenantId });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", res.Session.AccessToken);
        return res.Session.AccessToken;
    }
}
