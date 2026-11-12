using Backend.Features.FileManagement.Core;

namespace Backend.Features.FileManagement.Endpoints.Files;

/// <summary>
/// This endpoint exposes a DELETE operation to remove a previously uploaded file
/// by its unique filename.
/// </summary>
/// <remarks>
/// Authentication and <see cref="Allow.File_Delete"/> are required. The file's owner still decides
/// the answer: a file owned by another account is refused with
/// <see cref="ErrorCodes.PermissionDenied"/> and left untouched.
/// </remarks>
sealed class FileDeleteEndpoint(IFileService fileService) : Endpoint<FileDeleteRequest>
{
    public override void Configure()
    {
        Delete("{fileName}");
        Group<FileGroup>();
        Permissions(Allow.File_Delete);
    }

    public override async Task HandleAsync(FileDeleteRequest req, CancellationToken ct)
    {
        var result = await fileService.DeleteAsync(req.FileName, ct);

        if (result.Status == FileAccessStatus.NotFound)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (result.Status != FileAccessStatus.Granted)
        {
            this.ThrowError(ErrorCodes.PermissionDenied, StatusCodes.Status403Forbidden);
        }

        await Send.NoContentAsync(ct);
    }
}

/// <summary>
/// Request payload for <see cref="FileDeleteEndpoint"/>, identifying the file to
/// remove by its stored filename.
/// </summary>
public sealed class FileDeleteRequest
{
    public string FileName { get; set; } = null!;
}

/// <summary>
/// This validator validates the <see cref="FileDeleteRequest"/>.
/// </summary>
sealed class FileDeleteValidator : Validator<FileDeleteRequest>
{
    public FileDeleteValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .Must(fileName => fileName == Path.GetFileName(fileName))
            .WithMessage("The file name is invalid.");
    }
}
