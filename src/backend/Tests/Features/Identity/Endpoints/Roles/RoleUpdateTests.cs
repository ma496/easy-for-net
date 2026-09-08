namespace Backend.Tests.Features.Identity.Endpoints.Roles;

using Backend.Features.Identity.Core;
using Backend.Features.Identity.Endpoints.Roles;

/// <summary>
/// Tests for the <see cref="RoleUpdateEndpoint"/> covering updating roles, non-existent roles, and protected system-created roles.
/// </summary>
public class RoleUpdateTests(App app) : AppTestsBase(app)
{
    /// <summary>
    /// Verifies that a created role can be successfully updated with new name and description.
    /// </summary>
    [Fact]
    public async Task Update_Role()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<RoleCreateRequest>()
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var request = faker.Generate();
        var (createRsp, createRes) = await App.Client.POSTAsync<RoleCreateEndpoint, RoleCreateRequest, RoleCreateResponse>(request);

        createRsp.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateFaker = new Faker<RoleUpdateRequest>()
            .RuleFor(x => x.Id, f => createRes.Id)
            .RuleFor(x => x.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(x => x.Description, f => f.Lorem.Sentence());
        var updateRequest = updateFaker.Generate();
        var (updateRsp, updateRes) = await App.Client.PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, RoleUpdateResponse>(updateRequest);

        updateRsp.StatusCode.Should().Be(HttpStatusCode.OK);
        updateRes.Name.Should().Be(updateRequest.Name);
        updateRes.Description.Should().Be(updateRequest.Description);
    }

    /// <summary>
    /// Verifies that updating a non-existent role returns 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Update_NonExistent_Role()
    {
        await SetAuthTokenAsync();

        var faker = new Faker<RoleUpdateRequest>()
            .RuleFor(u => u.Id, f => Guid.NewGuid())
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var request = faker.Generate();
        var (updateRsp, _) = await App.Client.PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, RoleUpdateResponse>(request);

        updateRsp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Verifies that updating the system-created Admin role returns 400 Bad Request with <see cref="ErrorCodes.SystemCreatedRoleCannotBeUpdated"/>.
    /// </summary>
    [Fact]
    public async Task Update_Admin_Role_Should_Fail()
    {
        await SetAuthTokenAsync();

        // Get the system-created role ID - assuming you have a way to identify it
        var systemCreatedRole = await App.Services.GetRequiredService<IRoleService>()
            .Roles()
            .FirstAsync(r => r.Name == "Admin", cancellationToken: TestContext.Current.CancellationToken);

        var faker = new Faker<RoleUpdateRequest>()
            .RuleFor(u => u.Id, f => systemCreatedRole.Id)
            .RuleFor(u => u.Name, f => f.Internet.UserName() + f.UniqueIndex)
            .RuleFor(u => u.Description, f => f.Lorem.Sentence());
        var request = faker.Generate();
        var (updateRsp, res) = await App.Client.PUTAsync<RoleUpdateEndpoint, RoleUpdateRequest, ProblemDetails>(request);

        updateRsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors.Should().ContainSingle();
        res.Errors.First().Code.Should().Be(ErrorCodes.SystemCreatedRoleCannotBeUpdated);
    }
}