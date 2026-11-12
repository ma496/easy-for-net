using System.Text.Json.Nodes;
using Backend.Features.FileManagement.Core;
using Backend.Features.FileManagement.Endpoints.Files;
using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Identity.Core.Sessions;
using Backend.Features.Settings.Core;
using Backend.Tests.Fakes;

namespace Backend.Tests;

/// <summary>
/// Base class for all integration tests providing common setup, authentication, and helper methods.
/// </summary>
/// <remarks>
/// It derives from the plain <c>TestBase</c> rather than <c>TestBase&lt;App&gt;</c> because
/// <see cref="App"/> is an assembly fixture, not a class fixture - it is built once for the whole
/// run and handed to every test class's constructor. It carries no collection attribute either, so
/// xunit puts each test class in a collection of its own and runs them concurrently; a class that
/// has to be serialised against another names a collection for itself.
/// </remarks>
public abstract class AppTestsBase(App app) : TestBase
{
    protected readonly App App = app;

    private AsyncServiceScope _scope;
    private bool _scopeCreated;
    private HttpClient? _client;
    private readonly List<string> _uploadedFiles = [];

    /// <summary>
    /// The service scope this test acts in - one unit of work, like the one a request would open.
    /// </summary>
    /// <remarks>
    /// It is created on first use rather than in setup so that it cannot be missed by a derived class
    /// that overrides <c>SetupAsync</c> without calling this one. Resolving these services from the
    /// host's root provider instead would hand every test the same <see cref="AppDbContext"/> - one change
    /// tracker for the whole run, which concurrent tests would take from each other.
    /// </remarks>
    private IServiceProvider Scoped
    {
        get
        {
            if (!_scopeCreated)
            {
                _scope = App.Services.CreateAsyncScope();
                _scopeCreated = true;
            }

            return _scope.ServiceProvider;
        }
    }

    /// <summary>
    /// Resolves a service in this test's scope.
    /// </summary>
    protected T Service<T>() where T : notnull => Scoped.GetRequiredService<T>();

    /// <summary>
    /// This test's own HTTP client. The bearer token set on it belongs to this test and is seen by
    /// no other, which is what lets test classes run at the same time.
    /// </summary>
    protected HttpClient Client => _client ??= App.CreateClient(new ClientOptions());

    protected AppDbContext DbContext => Service<AppDbContext>();

    /// <summary>
    /// The session store the host runs on. Under the Testing environment it is one in-memory store shared by
    /// the whole run, so a session minted by a request is visible here and one deleted here is gone for
    /// the request that follows.
    /// </summary>
    protected ISessionStore SessionStore => Service<ISessionStore>();

    /// <summary>
    /// The stored session an access token names, looked up by the token's own <c>sid</c> claim.
    /// </summary>
    /// <param name="accessToken">The access token a sign-in, refresh or switch issued.</param>
    /// <returns>The stored session, or <see langword="null"/> when it no longer exists.</returns>
    protected async Task<SessionRecord?> SessionOfAsync(string accessToken)
        => await SessionStore.GetAsync(
            TestsHelper.PayloadOf(accessToken)[ClaimConstants.SessionId].GetString()!,
            TestContext.Current.CancellationToken);

    /// <summary>
    /// Releases this test's client and scope. An override in a derived class must call this one.
    /// </summary>
    protected override async ValueTask TearDownAsync()
    {
        _client?.Dispose();

        await DeleteUploadedFilesAsync();

        if (_scopeCreated)
        {
            await _scope.DisposeAsync();
        }

        await base.TearDownAsync();
    }

    /// <summary>
    /// Records a file this test uploaded so that its content is removed from storage when the test
    /// tears down. Call it straight after the upload, before any assertion, so a failing test cleans
    /// up too; a response that carries no stored name (a refused upload) is ignored.
    /// </summary>
    /// <param name="uploaded">What the upload answered with.</param>
    /// <remarks>
    /// Storage is one directory under the host's content root, shared by every test and every run, so
    /// an upload that is not removed outlives the run. Only the content is removed: its
    /// <c>StoredFile</c> row goes with the database when the run ends. A file the test already deleted
    /// is fine, since <see cref="IStorageProvider.DeleteAsync"/> does nothing for a missing name.
    /// </remarks>
    protected void DeleteAfterTest(FileUploadResponse? uploaded)
    {
        if (!string.IsNullOrWhiteSpace(uploaded?.FileName))
        {
            _uploadedFiles.Add(uploaded.FileName);
        }
    }

    /// <summary>
    /// Removes the content of every file this test recorded with <see cref="DeleteAfterTest"/>.
    /// </summary>
    /// <remarks>
    /// A failure is reported rather than thrown: cleanup must not fail a test that passed, and whatever
    /// is left is removed by <see cref="App"/> when the run ends.
    /// </remarks>
    private async Task DeleteUploadedFilesAsync()
    {
        if (_uploadedFiles.Count == 0)
        {
            return;
        }

        var storageProvider = Service<IStorageProvider>();
        foreach (var fileName in _uploadedFiles)
        {
            try
            {
                await storageProvider.DeleteAsync(fileName);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"The uploaded file {fileName} could not be deleted: {exception.Message}");
            }
        }

        _uploadedFiles.Clear();
    }

    /// <summary>
    /// Authenticates the HTTP client by setting a Bearer token obtained from the token endpoint. With no
    /// arguments the caller is the seeded administrator, who holds every permission.
    /// </summary>
    protected async Task SetAuthTokenAsync(string username = TestUsers.AdminUsername, string? password = null)
    {
        await TestsHelper.SetNewAuthTokenAsync(Client, username, password);
    }

    /// <summary>
    /// Makes every request <paramref name="client"/> sends see <paramref name="values"/> as the stored
    /// overrides of the setting named, by an overlay for that client's requests alone - never by writing
    /// the stored row, which every test running beside this one would see.
    /// </summary>
    /// <remarks>A client carries one overlay token, so the last call for a client wins.</remarks>
    /// <param name="client">The client whose requests should see the overlay.</param>
    /// <param name="settingName">The setting's registered name.</param>
    /// <param name="values">The overrides to report.</param>
    /// <returns>The same client, for chaining.</returns>
    protected HttpClient OverlaySetting(HttpClient client, string settingName, JsonObject values)
    {
        var token = Guid.NewGuid().ToString("N");
        Service<SettingOverlays>().ApplyForRequests(token, settingName, values);
        client.DefaultRequestHeaders.Remove(SettingOverlays.RequestHeader);
        client.DefaultRequestHeaders.Add(SettingOverlays.RequestHeader, token);
        return client;
    }

    /// <summary>
    ///     /// <summary>
    /// Opens sign-up for every request <paramref name="client"/> sends, with any further <c>General</c>
    /// values merged into the same overlay. Sign-up is open by default; this pins it for tests that
    /// also overlay other <c>General</c> values.
    /// </summary>
    /// <param name="client">The client whose requests should see sign-up open.</param>
    /// <param name="generalValues">Further <c>General</c> overrides for the same requests.</param>
    /// <returns>The same client, for chaining.</returns>
    protected HttpClient AllowSignup(HttpClient client, JsonObject? generalValues = null)
    {
        var values = generalValues is null ? [] : (JsonObject)generalValues.DeepClone();
        values["isSignupEnabled"] = true;
        return OverlaySetting(client, GeneralSettingsProvider.GeneralSettingName, values);
    }

    /// <summary>
    /// A visitor's client - no token, no cookie - whose requests see sign-up open (<see cref="AllowSignup"/>).
    /// </summary>
    protected HttpClient SignupVisitorClient()
        => AllowSignup(App.CreateClient(new ClientOptions { HandleCookies = false }));

    /// <summary>
    /// Clears the current authentication token from the HTTP client.
    /// </summary>
    protected void ClearAuthToken()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Creates a new admin user with the specified credentials, assigning the Admin role.
    /// </summary>
    /// <exception cref="Exception">Thrown when the admin role does not exist or the user already exists.</exception>
    protected async Task<User> CreateAdminUserAsync(string username, string password)
    {
        var userService = Service<IUserService>();
        var roleService = Service<IRoleService>();
        var user = await userService.GetByUsernameAsync(username);
        if (user == null)
        {
            user = await userService.CreateAsync(new User
            {
                SystemCreated = true,
                Username = username,
                Email = $"{username}@example.com"
            }, password);
            user.NormalizeProperties();
            // Assign admin role to the user
            var adminRole = await roleService.GetByNameAsync("Admin");
            if (adminRole == null)
            {
                throw new Exception("Admin role does not exist. Please ensure it is created before running the tests.");
            }
            await userService.AssignRoleAsync(user.Id, adminRole.Id);
            return user;
        }
        else
            throw new Exception($"Admin user ({username}) already exists. Please choose a different username.");
    }
}
