namespace Backend.Features.FileManagement.Endpoints.Files;

using Backend.Features.FileManagement.Core;

/// <summary>
/// This endpoint exposes a POST operation accepting a multipart file upload and
/// returning the unique filename under which the file was stored.
/// </summary>
/// <remarks>
/// Authentication is required - the endpoint declares no anonymous access, so an unauthenticated
/// caller is refused with the standard 401. That is not a policy decision so much as an arithmetic
/// one: every stored file is attributed either to a tenant or to an account, and an anonymous caller
/// has neither, so there would be nothing to attribute the upload to and nobody entitled to read it.
/// <para>
/// Usable with no tenant established, because it enforces the tenant rule itself rather than
/// escaping it. An account-owned upload - a profile image and the like - has to work while the caller
/// acts in any tenant or in none, so it cannot be behind the global requirement; a tenant-scoped
/// upload is instead refused here with <see cref="ErrorCodes.NoActiveTenant"/> when no tenant is
/// active, before the content is read, so a refused upload stores nothing.
/// </para>
/// </remarks>
sealed class FileUploadEndpoint(IFileService fileService, ITenantContext tenantContext) : Endpoint<FileUploadRequest, FileUploadResponse>
{
    /// <summary>
    /// The refusal reported for a tenant-scoped upload made with no tenant active. A file attributed
    /// to no tenant and to no account would belong to nobody, and nothing could ever read it again,
    /// so the upload is refused rather than stored unattributed.
    /// </summary>
    private const string NoActiveTenantMessage = "A tenant-scoped file can only be uploaded while acting in a tenant";

    public override void Configure()
    {
        Post("upload");
        Group<FileGroup>();
        AllowFileUploads();
    }

    public override async Task<FileUploadResponse> ExecuteAsync(FileUploadRequest req, CancellationToken ct)
    {
        // Settled before the stream is opened, because a refused upload has to leave nothing behind:
        // no content in storage, and no record pointing at content.
        if (!req.AccountOwned && !HasActiveTenant())
        {
            ThrowError(NoActiveTenantMessage, ErrorCodes.NoActiveTenant);
        }

        await using var stream = req.File!.OpenReadStream();
        var fileName = await fileService.UploadAsync(stream, req.File.FileName, req.File.ContentType, req.AccountOwned, ct);

        var response = new FileUploadResponse
        {
            FileName = fileName
        };

        return response;
    }

    /// <summary>
    /// Reports whether this request acts in a tenant that a file can be attributed to. Platform
    /// scope, and the unresolved scope a request reaching here outside the tenant pipeline would
    /// carry, both answer no: neither names a tenant, and reading either as "no tenant needed" is
    /// exactly how a tenant-scoped file would end up belonging to nobody.
    /// </summary>
    /// <returns><see langword="true"/> when a tenant is active for this request.</returns>
    private bool HasActiveTenant() => tenantContext.IsResolved && tenantContext.CurrentTenantId is not null;
}

/// <summary>
/// Request payload for <see cref="FileUploadEndpoint"/>, containing the uploaded
/// file as a multipart form part together with how it is to be attributed.
/// </summary>
public sealed class FileUploadRequest
{
    public IFormFile? File { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the file belongs to the calling account rather than to
    /// the tenant's data - a profile image and the like. Such a file is attributed to no tenant and to
    /// the calling account, may be uploaded while acting in any tenant or in none, and is read back by
    /// its owner alone. The default, <see langword="false"/>, attributes the file to the tenant active
    /// at the time of upload, which is then the only tenant it can ever be read in.
    /// </summary>
    public bool AccountOwned { get; set; }
}

/// <summary>
/// This validator validates the <see cref="FileUploadRequest"/>.
/// </summary>
sealed class FileUploadValidator : Validator<FileUploadRequest>
{
    public FileUploadValidator()
    {
        RuleFor(x => x.File).NotEmpty();
    }
}

/// <summary>
/// Response from <see cref="FileUploadEndpoint"/>, returning the unique filename
/// under which the uploaded file was stored.
/// </summary>
public sealed class FileUploadResponse
{
    public string FileName { get; set; } = null!;
}
