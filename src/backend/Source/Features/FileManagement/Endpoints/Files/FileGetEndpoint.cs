namespace Backend.Features.FileManagement.Endpoints.Files;

using System.Diagnostics.CodeAnalysis;
using Backend.Features.FileManagement.Core;

/// <summary>
/// This endpoint exposes a GET operation to stream a previously uploaded file
/// back to the caller, identified by its stored filename.
/// </summary>
/// <remarks>
/// Authentication is required - the endpoint declares no anonymous access, so an unauthenticated
/// caller is refused with the standard 401 and never reaches a stored file at all.
/// <para>
/// Usable with no tenant established, because an account-owned file, a profile image above
/// all, has to be readable by its owner while they act in any tenant or in none. The exemption is
/// from the global tenant requirement only: the file's own attribution still decides the answer, and
/// this endpoint reports each verdict distinctly - a name no record matches is a 404, a file
/// belonging to another tenant or another account is
/// <see cref="ErrorCodes.CrossTenantFileAccess"/>, and one belonging to a suspended or deleted
/// tenant is <see cref="ErrorCodes.TenantSuspended"/> or <see cref="ErrorCodes.TenantNotFound"/>,
/// its content retained but no longer served. A known or guessed stored file name therefore yields
/// nothing that is not the caller's own tenant's or account's.
/// </para>
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
            ThrowRefusal(result.Status);
        }

        await Send.StreamAsync(result.Content, contentType: result.ContentType, cancellation: ct);
    }

    /// <summary>
    /// Reports a refused read with the code that names why it was refused, so the caller can tell a
    /// file of another tenant from one whose tenant is out of service.
    /// </summary>
    /// <param name="status">The verdict reached for the file, never granted and never missing here.</param>
    [DoesNotReturn]
    private void ThrowRefusal(FileAccessStatus status)
    {
        var errorCode = status switch
        {
            FileAccessStatus.TenantSuspended => ErrorCodes.TenantSuspended,
            FileAccessStatus.TenantNotFound => ErrorCodes.TenantNotFound,
            // Cross-tenant access, and anything a later status might add: a verdict this endpoint
            // does not recognise is a refusal, never a reason to serve the content.
            _ => ErrorCodes.CrossTenantFileAccess
        };

        this.ThrowError(errorCode);
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
