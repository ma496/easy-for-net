using Backend.Features.FileManagement.Core;

namespace Backend.Features.FileManagement.Endpoints.Files;

/// <summary>
/// This endpoint exposes a POST operation accepting a multipart file upload and
/// returning the identifier and unique filename under which the file was stored.
/// </summary>
/// <remarks>
/// Authentication is required. The size of an upload is bounded by <c>Payload:MaximumSize</c> alone,
/// which the request pipeline enforces before this handler runs.
/// </remarks>
sealed class FileUploadEndpoint(IFileService fileService) : Endpoint<FileUploadRequest, FileUploadResponse>
{
    public override void Configure()
    {
        Post("upload");
        Group<FileGroup>();
        AllowFileUploads();
    }

    public override async Task<FileUploadResponse> ExecuteAsync(FileUploadRequest req, CancellationToken ct)
    {
        await using var stream = req.File!.OpenReadStream();
        var uploaded = await fileService.UploadAsync(stream, req.File.FileName, req.File.ContentType, req.AccountOwned, ct);

        return new FileUploadResponse
        {
            Id = uploaded.Id,
            FileName = uploaded.FileName
        };
    }
}

/// <summary>
/// Request payload for <see cref="FileUploadEndpoint"/>, containing the uploaded
/// file as a multipart form part together with who it belongs to.
/// </summary>
public sealed class FileUploadRequest
{
    public IFormFile? File { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the file belongs to the calling account - a profile
    /// image and the like - and is read back by its owner alone. The default, <see langword="false"/>,
    /// stores an application file any signed-in caller may read.
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
/// Response from <see cref="FileUploadEndpoint"/>, returning the identifier of the stored file - what
/// another record references it by - and the unique filename the file endpoints address it by.
/// </summary>
public sealed class FileUploadResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = null!;
}
