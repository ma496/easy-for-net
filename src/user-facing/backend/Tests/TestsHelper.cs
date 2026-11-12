using Backend.Features.Identity.Endpoints.Account;
using System.Net.Http.Headers;
using System.Text.Json;
using TokenRequest = Backend.Features.Identity.Endpoints.Account.TokenRequest;

namespace Backend.Tests;

public static class TestsHelper
{
    /// <summary>
    /// Signs in and returns the access token the caller was issued.
    /// </summary>
    public static async Task<string> GetNewAuthTokenAsync(HttpClient client, string username = TestUsers.AdminUsername, string? password = null)
    {
        var (_, res) = await client.POSTAsync<TokenEndpoint, TokenRequest, TokenResponse>(
            new() { Username = username, Password = password ?? TestUsers.AdminPassword });

        return res.AccessToken;
    }

    /// <summary>
    /// Signs in and leaves the client presenting the resulting token.
    /// </summary>
    public static async Task SetNewAuthTokenAsync(HttpClient client, string username = TestUsers.AdminUsername, string? password = null)
        => SetAuthToken(client, await GetNewAuthTokenAsync(client, username, password));

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
}
