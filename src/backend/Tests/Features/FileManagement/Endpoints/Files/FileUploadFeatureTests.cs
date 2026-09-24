namespace Backend.Tests.Features.FileManagement.Endpoints.Files;

using Backend.Features.FileManagement.Endpoints.Files;

/// <summary>
/// Tests for how <see cref="FileUploadEndpoint"/> holds an upload to the plan of the tenant it is made
/// in: file storage has to be included, and the file must be no larger than
/// <c>FileManagement.MaxFileSizeMb</c> allows. That applies to account-owned uploads made inside a
/// tenant too, while an upload made in platform scope is inside nobody's plan.
/// </summary>
/// <remarks>
/// Every value is written for a tenant the test created itself, so no other test's tenant is affected.
/// </remarks>
public class FileUploadFeatureTests(App app) : FileTestsBase(app)
{
    private const int OneMegabyte = 1024 * 1024;

    [Fact]
    public async Task An_Upload_Within_The_Plans_Size_Limit_Is_Stored()
    {
        var client = await MemberClientAsync(maxFileSizeMb: "1");

        var (response, _) = await client
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, FileUploadResponse>(UploadRequest("small"), sendAsFormData: true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_Upload_Larger_Than_The_Plan_Allows_Is_Refused(bool accountOwned)
    {
        var client = await MemberClientAsync(maxFileSizeMb: "1");
        var submittedName = $"{Guid.NewGuid():N}.txt";

        var (response, problem) = await client
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, ProblemDetails>(
                UploadRequest(new string('x', OneMegabyte + 1), submittedName, accountOwned: accountOwned),
                sendAsFormData: true);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        problem.Errors.Should().ContainSingle()
               .Which.Code.Should().Be(ErrorCodes.FeatureLimitExceeded);
        (await AnyStoredFileNamedAsync(submittedName)).Should().BeFalse("a refused upload stores nothing");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_Upload_Is_Refused_When_The_Plan_Excludes_File_Storage(bool accountOwned)
    {
        var client = await MemberClientAsync(fileStorage: "false");

        var (response, problem) = await client
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, ProblemDetails>(
                UploadRequest("anything", accountOwned: accountOwned),
                sendAsFormData: true);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        problem.Errors.Should().ContainSingle()
               .Which.Code.Should().Be(ErrorCodes.FeatureDisabled);
    }

    [Fact]
    public async Task An_Account_Owned_Upload_In_Platform_Scope_Is_Not_Held_To_Any_Plan()
    {
        await SetPlatformAdminAuthTokenAsync();

        var (response, _) = await Client
            .POSTAsync<FileUploadEndpoint, FileUploadRequest, FileUploadResponse>(
                UploadRequest(new string('x', OneMegabyte + 1), accountOwned: true),
                sendAsFormData: true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Creates a tenant with the plan values given and returns a client for one of its members.
    /// </summary>
    private async Task<HttpClient> MemberClientAsync(string? fileStorage = null, string? maxFileSizeMb = null)
    {
        var tenant = await CreateTenantAsync();
        var store = Service<IFeatureValueStore>();

        if (fileStorage is not null)
        {
            await store.SetAsync(FeatureNames.FileManagement_Enabled, fileStorage, FeatureValueProviderNames.Tenant,
                                 tenant.Id.ToString(), TestContext.Current.CancellationToken);
        }

        if (maxFileSizeMb is not null)
        {
            await store.SetAsync(FeatureNames.FileManagement_MaxFileSizeMb, maxFileSizeMb, FeatureValueProviderNames.Tenant,
                                 tenant.Id.ToString(), TestContext.Current.CancellationToken);
        }

        var member = await CreateTenantUserAsync(tenant.Id);
        return await ClientForAsync(member.Username, tenant.Id);
    }
}
