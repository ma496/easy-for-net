using Backend.Features.FileManagement.Core.Entities;
using Backend.Features.Identity.Core;
using Microsoft.AspNetCore.StaticFiles;

namespace Backend.Features.FileManagement.Core;

/// <summary>
/// Application-level service for uploading, downloading, and deleting files. Acts as a
/// thin coordinator over an <see cref="IStorageProvider"/>, adding unique filename generation and
/// ownership: every upload is recorded as a <see cref="StoredFile"/>, naming the owning account when
/// the file is account-owned, and every later read or delete is judged against that record rather
/// than against the name the caller supplied.
/// </summary>
[AllowOutside]
public interface IFileService
{
    /// <summary>
    /// Persists the supplied stream and returns the record under which it was saved.
    /// </summary>
    /// <param name="stream">The content to upload.</param>
    /// <param name="fileName">The original filename, used to derive the stored file's extension.</param>
    /// <param name="contentType">The MIME content type of the uploaded file.</param>
    /// <param name="accountOwned">
    /// <see langword="true"/> for a file that belongs to the caller's account - a profile image and
    /// the like - which only its owner reads back. <see langword="false"/>, the default, stores an
    /// application file any signed-in caller may read.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The identifier and unique filename assigned to the stored file.</returns>
    /// <exception cref="UserIdNullException">
    /// The upload is account-owned and there is no authenticated account to own it.
    /// </exception>
    Task<UploadedFile> UploadAsync(Stream stream, string fileName, string contentType, bool accountOwned = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the stored file against the caller and, when that grants access, returns its content
    /// stream and content type.
    /// </summary>
    /// <param name="fileName">The unique filename returned from a prior upload.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A result carrying <see cref="FileAccessStatus.Granted"/> together with the content, or the
    /// status that describes the refusal and no content.
    /// </returns>
    Task<FileDownloadResult> DownloadAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a stored file by its identifier with no access check at all. Only for content the caller
    /// has already decided is public - an image a published page references, say - because whoever
    /// can reach the caller reads the file.
    /// </summary>
    /// <param name="fileId">The <see cref="StoredFile"/> identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The content, or <see cref="FileAccessStatus.NotFound"/> when no such file is stored.</returns>
    Task<FileDownloadResult> OpenPublicAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the stored file against the caller exactly as a download does and, when that grants
    /// access, removes both the record and the content from storage.
    /// </summary>
    /// <param name="fileName">The unique filename of the file to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The status describing whether the file was deleted, and if not, why not.</returns>
    Task<FileDeleteResult> DeleteAsync(string fileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IFileService"/> implementation. It delegates the bytes to an
/// <see cref="IStorageProvider"/> and keeps the ownership question here.
/// </summary>
[NoDirectUse]
public class FileService(
    IStorageProvider storageProvider,
    AppDbContext dbContext,
    ICurrentUserService currentUserService) : IFileService
{
    /// <summary>
    /// Generates a unique filename from the record's identifier plus the original extension, records
    /// the file, and delegates persistence of the content to the storage provider.
    /// </summary>
    public async Task<UploadedFile> UploadAsync(Stream stream, string fileName, string contentType, bool accountOwned = false, CancellationToken cancellationToken = default)
    {
        // Ownership is settled before anything is written: an upload that may not be owned fails
        // here, with no orphaned bytes left in storage.
        var id = Guid.NewGuid();
        var storedFile = new StoredFile
        {
            Id = id,
            OwnerUserId = accountOwned ? RequireCurrentUserId() : null,
            FileName = $"{id}{Path.GetExtension(fileName)}",
            OriginalFileName = fileName,
            ContentType = contentType
        };

        await storageProvider.SaveAsync(stream, storedFile.FileName, contentType);

        try
        {
            dbContext.StoredFiles.Add(storedFile);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Left tracked as added, the record that just failed to save would be retried by the next
            // save on this context.
            dbContext.Entry(storedFile).State = EntityState.Detached;

            // Content stored without a record is unreachable forever. Remove it best-effort, then let
            // the original failure surface.
            try
            {
                await storageProvider.DeleteAsync(storedFile.FileName);
            }
            catch
            {
                // Ignored - see above. The orphaned content is unreachable, never served.
            }

            throw;
        }

        return new UploadedFile(storedFile.Id, storedFile.FileName);
    }

    /// <summary>
    /// Resolves the file and, when access is granted, retrieves its content stream and the content
    /// type recorded at upload time.
    /// </summary>
    public async Task<FileDownloadResult> DownloadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var (status, storedFile) = await ResolveAsync(fileName, tracked: false, cancellationToken);
        if (storedFile is null)
        {
            return new FileDownloadResult { Status = status };
        }

        return await OpenAsync(storedFile);
    }

    /// <inheritdoc />
    public async Task<FileDownloadResult> OpenPublicAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var storedFile = await dbContext.StoredFiles
            .AsNoTracking()
            .Where(f => f.Id == fileId)
            .Select(f => new StoredFile
            {
                Id = f.Id,
                OwnerUserId = f.OwnerUserId,
                FileName = f.FileName,
                OriginalFileName = f.OriginalFileName,
                ContentType = f.ContentType
            })
            .FirstOrDefaultAsync(cancellationToken);

        return storedFile is null
            ? new FileDownloadResult { Status = FileAccessStatus.NotFound }
            : await OpenAsync(storedFile);
    }

    /// <summary>
    /// Resolves the file and, when access is granted, removes the record and then the content.
    /// </summary>
    public async Task<FileDeleteResult> DeleteAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var (status, storedFile) = await ResolveAsync(fileName, tracked: true, cancellationToken);
        if (storedFile is null)
        {
            return new FileDeleteResult { Status = status };
        }

        // The record goes first. While it exists the file is reachable, so a failure between the two
        // steps leaves a readable file rather than a record pointing at nothing.
        dbContext.StoredFiles.Remove(storedFile);
        await dbContext.SaveChangesAsync(cancellationToken);

        await storageProvider.DeleteAsync(storedFile.FileName);

        return new FileDeleteResult { Status = FileAccessStatus.Granted };
    }

    private async Task<FileDownloadResult> OpenAsync(StoredFile storedFile)
    {
        var content = await storageProvider.GetAsync(storedFile.FileName);
        if (content is null)
        {
            return new FileDownloadResult { Status = FileAccessStatus.NotFound };
        }

        return new FileDownloadResult
        {
            Status = FileAccessStatus.Granted,
            Content = content,
            // The type the file was uploaded with is more faithful than one re-derived from the
            // extension; the derivation stays as the fallback for a record that carries none.
            ContentType = string.IsNullOrWhiteSpace(storedFile.ContentType)
                ? GetContentType(storedFile.FileName)
                : storedFile.ContentType,
            OriginalFileName = storedFile.OriginalFileName
        };
    }

    /// <summary>
    /// Finds the record for a stored file name and decides whether this caller may have it: an
    /// application file is anybody's who is signed in, an account-owned one its owner's alone.
    /// </summary>
    private async Task<(FileAccessStatus Status, StoredFile? File)> ResolveAsync(string fileName, bool tracked, CancellationToken cancellationToken)
    {
        var query = dbContext.StoredFiles.Where(f => f.FileName == fileName);

        var storedFile = tracked
            ? await query.FirstOrDefaultAsync(cancellationToken)
            : await query
                .AsNoTracking()
                .Select(f => new StoredFile
                {
                    Id = f.Id,
                    OwnerUserId = f.OwnerUserId,
                    FileName = f.FileName,
                    OriginalFileName = f.OriginalFileName,
                    ContentType = f.ContentType
                })
                .FirstOrDefaultAsync(cancellationToken);

        if (storedFile is null)
        {
            return (FileAccessStatus.NotFound, null);
        }

        if (storedFile.OwnerUserId is { } ownerUserId && ownerUserId != currentUserService.GetCurrentUserId())
        {
            return (FileAccessStatus.AccessDenied, null);
        }

        return (FileAccessStatus.Granted, storedFile);
    }

    /// <summary>
    /// Returns the account an account-owned upload belongs to, refusing an unauthenticated caller.
    /// </summary>
    /// <exception cref="UserIdNullException">The request carries no authenticated account.</exception>
    private Guid RequireCurrentUserId()
        => currentUserService.GetCurrentUserId() ?? throw new UserIdNullException();

    private static string GetContentType(string path)
    {
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(path, out var contentType))
        {
            contentType = "application/octet-stream";
        }
        return contentType;
    }
}

/// <summary>
/// The identity of a file that has just been stored: its record's identifier and the unique name the
/// file-management endpoints address it by.
/// </summary>
/// <param name="Id">The <see cref="StoredFile"/> identifier.</param>
/// <param name="FileName">The unique stored file name.</param>
[AllowOutside]
public sealed record UploadedFile(Guid Id, string FileName);

/// <summary>
/// Result of a download attempt, bundling the verdict with the file's content stream and its MIME
/// type. Content is present only when <see cref="Status"/> is <see cref="FileAccessStatus.Granted"/>.
/// </summary>
[AllowOutside]
public sealed class FileDownloadResult
{
    public FileAccessStatus Status { get; init; }
    public Stream Content { get; init; } = Stream.Null;
    public string ContentType { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
}

/// <summary>
/// Result of a delete attempt. It carries only the verdict: the file was removed, or it was left
/// alone for the reason named.
/// </summary>
[AllowOutside]
public sealed class FileDeleteResult
{
    public FileAccessStatus Status { get; init; }
}
