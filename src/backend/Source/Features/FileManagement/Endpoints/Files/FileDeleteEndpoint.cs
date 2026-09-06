namespace Backend.Features.FileManagement.Endpoints.Files;

using Backend.Features.FileManagement.Core;

/// <summary>
/// This endpoint exposes a DELETE operation to remove a previously uploaded file
/// by its unique filename.
/// </summary>
public class FileDeleteEndpoint(IFileService fileService) : Endpoint<FileDeleteRequest>
{
    public override void Configure()
    {
        Delete("{fileName}");
        Group<FileGroup>();
        Permissions(Allow.File_Delete);
    }

    public override async Task HandleAsync(FileDeleteRequest req, CancellationToken ct)
    {
        await fileService.DeleteAsync(req.FileName);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>
/// Request payload for <see cref="FileDeleteEndpoint"/>, identifying the file to
/// remove by its stored filename.
/// </summary>
public class FileDeleteRequest
{
    public string FileName { get; set; } = null!;
}

/// <summary>
/// This validator validates the <see cref="FileDeleteRequest"/>.
/// </summary>
public class FileDeleteRequestValidator : Validator<FileDeleteRequest>
{
    public FileDeleteRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .Must(fileName => fileName == Path.GetFileName(fileName))
            .WithMessage("The file name is invalid.");
    }
}
