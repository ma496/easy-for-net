namespace Backend.Features.FileManagement.Endpoints.Files;

using Backend.Features.FileManagement.Core;

/// <summary>
/// This endpoint exposes a GET operation to stream a previously uploaded file
/// back to the caller, identified by its stored filename.
/// </summary>
public class FileGetEndpoint(IFileService fileService) : Endpoint<FileGetRequest>
{
    public override void Configure()
    {
        Get("{fileName}");
        Group<FileGroup>();
    }

    public override async Task HandleAsync(FileGetRequest req, CancellationToken ct)
    {
        var result = await fileService.DownloadAsync(req.FileName);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.StreamAsync(result.Content, contentType: result.ContentType, cancellation: ct);
    }
}

/// <summary>
/// Request payload for <see cref="FileGetEndpoint"/>, identifying the file to stream
/// by its stored filename.
/// </summary>
public class FileGetRequest
{
    public string FileName { get; set; } = null!;
}

/// <summary>
/// This validator validates the <see cref="FileGetRequest"/>.
/// </summary>
public class FileGetValidator : Validator<FileGetRequest>
{
    public FileGetValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .Must(fileName => fileName == Path.GetFileName(fileName))
            .WithMessage("The file name is invalid.");
    }
}
