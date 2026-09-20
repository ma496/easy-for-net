namespace Backend.Tests.Features.Identity.Endpoints.Account;

using System.Text;
using Backend.Data.Entities;
using Backend.Features.FileManagement.Core.Entities;
using Backend.Features.FileManagement.Endpoints.Files;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Endpoints.Account;
using Backend.Features.Identity.Endpoints.Users;
using Backend.Tests.Features.Tenancy;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Tests that the account self-service flows stay usable for an authenticated caller acting in no
/// tenant - sign-up, the verification email, password recovery, the password change, the profile read
/// and edit, and setting and viewing a profile image (AC-051, AC-097).
/// </summary>
/// <remarks>
/// <para>
/// These are the surfaces that belong to the person rather than to the tenant, which is why they are
/// the ones a caller with no usable membership is still owed: an account that has joined nothing yet
/// has to be able to manage itself, or the only way out of that state would be through a surface it
/// cannot reach.
/// </para>
/// <para>
/// Every row is stated by a caller that genuinely has no tenant, and reaches that state the way it
/// actually arises: the account signs in to the one tenant it belongs to, that tenant is put out of
/// service, and its session is renewed - which hands back a session naming no tenant. An ordinary
/// account cannot sign in without one, so this is the state, not an account that never had a tenant.
/// Each row proves the standing before it exercises its flow, by taking the refusal a tenant-scoped
/// call gives that same caller.
/// </para>
/// <para>
/// A purpose-built account and tenant are used rather than seeded ones because two of these flows
/// change the account they run against - the password change and the profile edit - and the tenant is
/// suspended, which no seeded tenant may be. A fresh pair per row keeps every write the test makes its
/// own, which is what the suite's parallel-safety rules require.
/// </para>
/// </remarks>
public class AccountSelfServiceTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The password the self-service rows change to, which no other test knows or uses.
    /// </summary>
    private const string ChangedPassword = "Changed#123";

    /// <summary>
    /// Every account self-service flow that has to work without an active tenant, under the name each
    /// is known by, so that a flow added to the endpoint surface without a row here leaves a gap
    /// rather than passing unnoticed.
    /// </summary>
    public static TheoryData<string> SelfServiceFlows => new()
    {
        "signup",
        "resend-verify-email",
        "forget-password",
        "change-password",
        "profile",
        "update-profile",
        "profile-image"
    };

    /// <summary>
    /// Verifies that the named self-service flow is answered for a caller acting in no tenant, while a
    /// tenant-scoped call by that same caller is refused for want of one (AC-051, AC-097).
    /// </summary>
    /// <param name="flow">The flow to exercise.</param>
    [Theory]
    [MemberData(nameof(SelfServiceFlows))]
    public async Task Self_Service_Flows_Work_Without_A_Tenant(string flow)
    {
        var (account, client) = await CallerWithNoTenantAsync();

        // The standing, taken first so the flow below is read against a caller that really is in it.
        var (refused, refusal) = await client
            .GETAsync<UserListEndpoint, UserListRequest, ProblemDetails>(new());

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "this caller acts in no tenant and so holds no permission at all, which is the state the flow below has to survive");
        refusal.Errors.Should().ContainSingle();
        refusal.Errors.First().Code.Should().Be(ErrorCodes.PermissionDenied);

        var statuses = await RunFlowAsync(flow, client, account);

        statuses.Should().NotBeEmpty();

        statuses.Where(status => status != HttpStatusCode.OK).Should().BeEmpty(
            "{0} is account self-service, so every call it makes is answered for a caller acting in no tenant - the refusal above is the one answer none of them may give",
            flow);
    }

    /// <summary>
    /// An authenticated caller acting in no tenant, reached the way that state actually arises: the
    /// account signs in to its only tenant, the tenant is suspended, and the session is renewed - which
    /// drops the tenant while leaving the caller signed in.
    /// </summary>
    /// <returns>The account, and a client presenting its tenant-less session.</returns>
    private async Task<(User Account, HttpClient Client)> CallerWithNoTenantAsync()
    {
        var tenant = await CreateTenantAsync();
        var account = await CreateTenantUserAsync(tenant.Id, await CreateTenantRoleAsync(tenant.Id, Allow.User_View));
        var session = await SessionForAsync(account.Username, tenant.Id);

        await TenantScopedAsync(tenant.Id, async () =>
        {
            var row = await DbContext.Tenants.SingleAsync(candidate => candidate.Id == tenant.Id, TestContext.Current.CancellationToken);
            row.Status = TenantStatus.Suspended;
            await DbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        await session.RenewAsync();

        return (account, session.Client);
    }

    /// <summary>
    /// Runs one self-service flow and reports the status of every call it made, so that a flow which
    /// takes several steps is judged on all of them rather than on its last.
    /// </summary>
    /// <param name="flow">The flow to run.</param>
    /// <param name="client">The client acting as the caller with no tenant.</param>
    /// <param name="account">That caller's account.</param>
    /// <returns>The status of each call the flow made, in the order it made them.</returns>
    private async Task<List<HttpStatusCode>> RunFlowAsync(string flow, HttpClient client, User account)
        => flow switch
        {
            "signup" => await SignUpAsync(client),
            "resend-verify-email" => await ResendVerifyEmailAsync(client, account),
            "forget-password" => await ForgetPasswordAsync(client, account),
            "change-password" => await ChangePasswordAsync(client),
            "profile" => await ReadProfileAsync(client, account),
            "update-profile" => await UpdateProfileAsync(client, account),
            "profile-image" => await SetAndViewProfileImageAsync(client, account),
            _ => throw new ArgumentOutOfRangeException(nameof(flow), flow, "no such account self-service flow")
        };

    /// <summary>
    /// Signs a visitor up while presenting a session that acts in no tenant - the other reading of
    /// AC-119, alongside the anonymous visitor: neither a tenant nor the absence of a session stops it.
    /// </summary>
    /// <param name="client">The client to sign up with.</param>
    /// <returns>The status of the call.</returns>
    private static async Task<List<HttpStatusCode>> SignUpAsync(HttpClient client)
    {
        var username = $"signup-{Guid.NewGuid():N}";

        var (response, _) = await client
            .POSTAsync<SignupEndpoint, SignupRequest, SignupResponse>(new()
            {
                Username = username,
                Email = $"{username}@example.com",
                Password = "Signup#123",
                ConfirmPassword = "Signup#123",
                TenantName = $"Tenant {Guid.NewGuid():N}",
                TenantIdentifier = NewTenantIdentifier()
            });

        return [response.StatusCode];
    }

    /// <summary>
    /// Asks for the verification email to be re-issued. The Testing configuration does not require a
    /// verified address, so the endpoint answers as soon as it is reached, which is exactly what makes
    /// its reachability the thing under test.
    /// </summary>
    /// <param name="client">The client to call with.</param>
    /// <param name="account">The account the request names.</param>
    /// <returns>The status of the call.</returns>
    private static async Task<List<HttpStatusCode>> ResendVerifyEmailAsync(HttpClient client, User account)
    {
        var (response, _) = await client
            .POSTAsync<ResendVerifyEmailEndpoint, ResendVerifyEmailRequest, EmptyResponse>(
                new() { EmailOrUsername = account.Username });

        return [response.StatusCode];
    }

    /// <summary>
    /// Starts password recovery for the account, which is the branch that actually issues a reset token
    /// rather than the deliberately indistinguishable answer given for an address nobody holds.
    /// </summary>
    /// <param name="client">The client to call with.</param>
    /// <param name="account">The account to recover.</param>
    /// <returns>The status of the call.</returns>
    private static async Task<List<HttpStatusCode>> ForgetPasswordAsync(HttpClient client, User account)
    {
        var (response, _) = await client
            .POSTAsync<ForgetPasswordEndpoint, ForgetPasswordRequest, EmptyResponse>(new() { Email = account.Email });

        return [response.StatusCode];
    }

    /// <summary>
    /// Changes the account's own password. The account belongs to no tenant and its password is
    /// nobody else's, so the change costs no other test anything.
    /// </summary>
    /// <param name="client">The client to call with.</param>
    /// <returns>The status of the call.</returns>
    private static async Task<List<HttpStatusCode>> ChangePasswordAsync(HttpClient client)
    {
        var (response, _) = await client
            .POSTAsync<ChangePasswordEndpoint, ChangePasswordRequest, EmptyResponse>(new()
            {
                CurrentPassword = TestUsers.DefaultPassword,
                NewPassword = ChangedPassword
            });

        return [response.StatusCode];
    }

    /// <summary>
    /// Reads the account's own profile.
    /// </summary>
    /// <param name="client">The client to call with.</param>
    /// <param name="account">The account the profile has to be.</param>
    /// <returns>The status of the call.</returns>
    private static async Task<List<HttpStatusCode>> ReadProfileAsync(HttpClient client, User account)
    {
        var (response, profile) = await client.GETAsync<ProfileEndpoint, UserProfileResponse>();

        profile.Id.Should().Be(account.Id, "the profile read is the caller's own, whatever tenant they are or are not acting in");

        return [response.StatusCode];
    }

    /// <summary>
    /// Edits the account's own profile.
    /// </summary>
    /// <param name="client">The client to call with.</param>
    /// <param name="account">The account being edited.</param>
    /// <returns>The status of the call.</returns>
    private static async Task<List<HttpStatusCode>> UpdateProfileAsync(HttpClient client, User account)
    {
        var (response, updated) = await client
            .POSTAsync<UpdateProfileEndpoint, UserUpdateProfileRequest, UserUpdateProfileResponse>(new()
            {
                Email = account.Email,
                FirstName = "Self",
                LastName = "Service"
            });

        updated.FirstName.Should().Be("Self");

        return [response.StatusCode];
    }

    /// <summary>
    /// Uploads a file as the account's own image, sets it on the profile and reads the profile back.
    /// The file is uploaded with <see cref="FileUploadRequest.AccountOwned"/> set, which is what makes
    /// it belong to the account rather than to a tenant - and the caller is acting in no tenant at all,
    /// which is one of the two standings AC-097 requires such a file to be usable in (AC-097).
    /// </summary>
    /// <param name="client">The client to call with.</param>
    /// <param name="account">The account that owns the image.</param>
    /// <returns>The status of each call, in order.</returns>
    private async Task<List<HttpStatusCode>> SetAndViewProfileImageAsync(HttpClient client, User account)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"image of {account.Username}"));
        var upload = new FileUploadRequest
        {
            File = new FormFile(stream, 0, stream.Length, "file", "avatar.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png"
            },
            AccountOwned = true
        };

        var (uploadResponse, uploaded) = await client
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, FileUploadResponse>(upload, sendAsFormData: true);

        uploaded.FileName.Should().NotBeNullOrWhiteSpace();

        // Attributed to no tenant and to the calling account: that pair is what makes the file the
        // account's own rather than tenant data, and it is why the upload was answered at all.
        var stored = await DbContext.StoredFiles
            .AcrossAllTenants()
            .AsNoTracking()
            .SingleAsync(file => file.FileName == uploaded.FileName, TestContext.Current.CancellationToken);

        stored.TenantId.Should().BeNull("an account-owned file is attributed to no tenant");
        stored.OwnerUserId.Should().Be(account.Id, "it is attributed to the account that uploaded it");

        var (setResponse, _) = await client
            .POSTAsync<UpdateProfileEndpoint, UserUpdateProfileRequest, UserUpdateProfileResponse>(new()
            {
                Email = account.Email,
                Image = uploaded.FileName
            });

        var (viewResponse, profile) = await client.GETAsync<ProfileEndpoint, UserProfileResponse>();

        profile.Image.Should().Be(uploaded.FileName, "the image just set is the image the profile reports");

        return [uploadResponse.StatusCode, setResponse.StatusCode, viewResponse.StatusCode];
    }
}
