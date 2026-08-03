using Backend.Features.Identity.Core;
using Backend.Features.Identity.Core.Entities;

namespace Backend.Tests;

[Collection("SharedContext")]
/// <summary>
/// Base class for all integration tests providing common setup, authentication, and helper methods.
/// </summary>
public abstract class AppTestsBase(App app) : TestBase<App>
{
    protected readonly App App = app;
    protected readonly AppDbContext DbContext = app.Services.GetRequiredService<AppDbContext>();

    /// <summary>
    /// Skips the current test if the shared fixture initialization has failed.
    /// Called by xUnit before each test method.
    /// </summary>
    protected override async ValueTask SetupAsync()
    {
        if (SharedContextFixture.InitializationError is { } error)
            Assert.Skip(error);
        await base.SetupAsync();
    }

    /// <summary>
    /// Authenticates the HTTP client by setting a Bearer token obtained from the token endpoint.
    /// </summary>
    protected async Task SetAuthTokenAsync(string username = "admin", string password = "Admin#123")
    {
        await TestsHelper.SetNewAuthTokenAsync(App.Client, username, password);
    }

    /// <summary>
    /// Clears the current authentication token from the HTTP client.
    /// </summary>
    protected void ClearAuthToken()
    {
        App.Client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Creates a new admin user with the specified credentials, assigning the Admin role.
    /// </summary>
    /// <exception cref="Exception">Thrown when the admin role does not exist or the user already exists.</exception>
    protected async Task<User> CreateAdminUserAsync(string username, string password)
    {
        var userService = App.Services.GetRequiredService<IUserService>();
        var roleService = App.Services.GetRequiredService<IRoleService>();
        var user = await userService.GetByUsernameAsync(username);
        if (user == null)
        {
            user = await userService.CreateAsync(new User
            {
                Default = true,
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
