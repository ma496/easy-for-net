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
/// Marked <see cref="AllowNoTenantAttribute"/> because an account-owned file, a profile image above
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
[AllowNoTenant]
sealed class FileGetEndpoint(IFileService fileService) : Endpoint<FileGetRequest>
{
    /// <summary>
    /// The refusal reported for a file attributed to another tenant, or owned by another account. It
    /// names neither, so it discloses nothing about the file beyond the fact that it is not the
    /// caller's to read.
    /// </summary>
    private const string CrossTenantAccessMessage = "The file belongs to another tenant";

    /// <summary>
    /// The refusal reported for a file whose tenant is suspended. The file is retained and simply
    /// stops being served until the tenant is reactivated.
    /// </summary>
    private const string TenantSuspendedMessage = "The tenant this file belongs to is suspended";

    /// <summary>
    /// The refusal reported for a file whose tenant no longer exists. As with suspension the content
    /// is retained; there is simply no live tenant left to serve it in.
    /// </summary>
    private const string TenantNotFoundMessage = "The tenant this file belongs to no longer exists";

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
        var (message, errorCode) = status switch
        {
            FileAccessStatus.TenantSuspended => (TenantSuspendedMessage, ErrorCodes.TenantSuspended),
            FileAccessStatus.TenantNotFound => (TenantNotFoundMessage, ErrorCodes.TenantNotFound),
            // Cross-tenant access, and anything a later status might add: a verdict this endpoint
            // does not recognise is a refusal, never a reason to serve the content.
            _ => (CrossTenantAccessMessage, ErrorCodes.CrossTenantFileAccess)
        };

        ThrowError(message, errorCode);
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
