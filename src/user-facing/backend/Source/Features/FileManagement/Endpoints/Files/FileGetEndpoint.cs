using Backend.Features.FileManagement.Core;

namespace Backend.Features.FileManagement.Endpoints.Files;

/// <summary>
/// This endpoint exposes a GET operation to stream a previously uploaded file
/// back to the caller, identified by its stored filename.
/// </summary>
/// <remarks>
/// Authentication is required. A name no record matches is a 404, and a file owned by another
/// account is <see cref="ErrorCodes.PermissionDenied"/>.
/// </remarks>
sealed class FileGetEndpoint(IFileService fileService) : Endpoint<FileGetRequest>
{
    public override void Configure()
    {
        Get("{fileName}");
        Group<FileGroup>();
    }

    public override async Task HandleAsync(FileGetRequest req, CancellationToken ct)
    {
        var result = await fileService.DownloadAsync(req.FileName, ct);

        if (result.Status == FileAccessStatus.NotFound)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (result.Status != FileAccessStatus.Granted)
        {
            this.ThrowError(ErrorCodes.PermissionDenied, StatusCodes.Status403Forbidden);
        }

        await Send.StreamAsync(result.Content, contentType: result.ContentType, cancellation: ct);
    }
}

/// <summary>
/// Request payload for <see cref="FileGetEndpoint"/>, identifying the file to stream
/// by its stored filename.
/// </summary>
public sealed class FileGetRequest
{
    public string FileName { get; set; } = null!;
}

/// <summary>
/// This validator validates the <see cref="FileGetRequest"/>.
/// </summary>
sealed class FileGetValidator : Validator<FileGetRequest>
{
    public FileGetValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .Must(fileName => fileName == Path.GetFileName(fileName))
            .WithMessage("The file name is invalid.");
    }
}
