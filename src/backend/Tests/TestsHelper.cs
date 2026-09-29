namespace Backend.Tests;

using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Tenancy.Endpoints.Tenants;
using System.Net.Http.Headers;
using System.Text.Json;

public static class TestsHelper
{
    /// <summary>
    /// Signs in and returns the access token the caller was issued, naming the tenant to start the
    /// session in when one is given. Sign-in resolves a tenant by itself only when exactly one active
    /// membership stands, and refuses an ordinary account outright when none or several do, so an
    /// account holding anything other than one membership has to name the tenant it means here.
    /// </summary>
    public static async Task<string> GetNewAuthTokenAsync(HttpClient client, string username = TestUsers.TenantAdminUsername, string password = TestUsers.AdminPassword, string? tenantIdentifier = null)
    {
        var (_, res) = await client.POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(
            new() { Username = username, Password = password, TenantIdentifier = tenantIdentifier });

        return res.AccessToken;
    }

    /// <summary>
    /// Signs in, naming a tenant when one is given, and leaves the client presenting the resulting token.
    /// </summary>
    public static async Task SetNewAuthTokenAsync(HttpClient client, string username = TestUsers.TenantAdminUsername, string password = TestUsers.AdminPassword, string? tenantIdentifier = null)
        => SetAuthToken(client, await GetNewAuthTokenAsync(client, username, password, tenantIdentifier));

    /// <summary>
    /// Leaves the client presenting this token on every later request.
    /// </summary>
    public static void SetAuthToken(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// <summary>
    /// The claims an access token carries, read out of the token itself - its payload, decoded, with no
    /// signature check - so what is asserted is what the token was minted with rather than what a later
    /// request made of it.
    /// </summary>
    /// <param name="accessToken">The token to read.</param>
    /// <returns>Each payload property by its literal name.</returns>
    public static Dictionary<string, JsonElement> PayloadOf(string accessToken)
    {
        var payload = accessToken.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        using var document = JsonDocument.Parse(Convert.FromBase64String(payload));

        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone());
    }

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
