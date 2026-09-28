namespace Backend.Features.FileManagement.Endpoints.Files;

using System.Diagnostics.CodeAnalysis;
using Backend.Features.FileManagement.Core;

/// <summary>
/// This endpoint exposes a DELETE operation to remove a previously uploaded file
/// by its unique filename.
/// </summary>
/// <remarks>
/// Authentication is required - the endpoint declares no anonymous access, so an unauthenticated
/// caller is refused with the standard 401 - and the <see cref="Allow.File_Delete"/> permission it
/// has always declared is unchanged.
/// <para>
/// Usable with no tenant established, so that an account-owned file, a profile image above
/// all, can be removed by its owner while they act in any tenant or in none. The exemption is from
/// the global tenant requirement only: the file's own attribution still decides the answer, and it
/// is resolved exactly as a read resolves it, so a file belonging to another tenant or another
/// account is refused with <see cref="ErrorCodes.CrossTenantFileAccess"/> and left untouched, and
/// one belonging to a suspended or deleted tenant is refused and retained. Replacing a file is a
/// deletion followed by an upload, so these refusals govern replacement too.
/// </para>
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
            ThrowRefusal(result.Status);
        }

        await Send.NoContentAsync(ct);
    }

    /// <summary>
    /// Reports a refused deletion with the code that names why it was refused, so the caller can
    /// tell a file of another tenant from one whose tenant is out of service.
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
            // does not recognise is a refusal, never a reason to remove the file.
            _ => ErrorCodes.CrossTenantFileAccess
        };

        this.ThrowError(errorCode);
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
