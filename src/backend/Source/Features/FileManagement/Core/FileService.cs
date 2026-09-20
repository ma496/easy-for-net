namespace Backend.Features.FileManagement.Core;

using Backend.Features.Tenancy.Core;
using Backend.Features.FileManagement.Core.Entities;
using Backend.Features.Identity.Core;
using Microsoft.AspNetCore.StaticFiles;

/// <summary>
/// Application-level service for uploading, downloading, and deleting files. Acts as a
/// thin coordinator over an <see cref="IStorageProvider"/>, adding concerns such as
/// unique filename generation and, above all, attribution: every upload is recorded as a
/// <see cref="StoredFile"/> naming either the tenant it was uploaded in or the account that owns
/// it, and every later read or delete is judged against that record rather than against the name
/// the caller supplied. A stored file name is therefore only an address, never a permission:
/// knowing or guessing one yields nothing that is not attributed to the caller's own tenant or
/// account.
/// </summary>
[AllowOutside]
public interface IFileService
{
    /// <summary>
    /// Persists the supplied stream and returns a unique filename under which it was saved,
    /// recording the file's attribution alongside it.
    /// </summary>
    /// <param name="stream">The content to upload.</param>
    /// <param name="fileName">The original filename, used to derive the stored file's extension.</param>
    /// <param name="contentType">The MIME content type of the uploaded file.</param>
    /// <param name="accountOwned">
    /// <see langword="true"/> for a file that belongs to the caller's account rather than to a
    /// tenant's data - a profile image and the like. Such a file is attributed to no tenant and to
    /// the calling account, and its owner reads it while acting in any tenant or in none.
    /// <see langword="false"/>, the default, attributes the file to the tenant active at the time
    /// of upload, which is then the only tenant it can ever be read in.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The unique filename assigned to the stored file.</returns>
    /// <remarks>
    /// Attribution is settled before a single byte is written, so a refused upload stores nothing.
    /// A tenant-scoped upload with no tenant active is a programming error here rather than a user
    /// error: the endpoint refuses that request first, with the defined "no active tenant" code, so
    /// reaching this method without a tenant means the caller skipped that check.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The upload is tenant-scoped and the active scope names no tenant.
    /// </exception>
    /// <exception cref="TenantScopeNotEstablishedException">
    /// The upload is tenant-scoped and no scope has been established at all.
    /// </exception>
    /// <exception cref="UserIdNullException">
    /// The upload is account-owned and there is no authenticated account to own it.
    /// </exception>
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, bool accountOwned = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the stored file's attribution against the caller and, when that grants access,
    /// returns its content stream and content type. The returned result always says why access was
    /// or was not granted, so a caller can tell a file that does not exist from one that exists but
    /// belongs elsewhere, and report each of them differently.
    /// </summary>
    /// <param name="fileName">The unique filename returned from a prior upload.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A result carrying <see cref="FileAccessStatus.Granted"/> together with the content, or the
    /// status that describes the refusal and no content.
    /// </returns>
    Task<FileDownloadResult> DownloadAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the stored file's attribution against the caller exactly as a download does and,
    /// when that grants access, removes both the attribution record and the content from storage.
    /// A file attributed elsewhere is left untouched and the refusal is reported instead.
    /// </summary>
    /// <param name="fileName">The unique filename of the file to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>The status describing whether the file was deleted, and if not, why not.</returns>
    Task<FileDeleteResult> DeleteAsync(string fileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Verdict of resolving one stored file's attribution against the caller and the tenant they are
/// acting in. Every refusal is a distinct value rather than a bare "no", because the caller has to
/// answer a missing file differently from one that exists but belongs to another tenant, and both
/// differently from one whose tenant is no longer being served.
/// </summary>
public enum FileAccessStatus
{
    /// <summary>
    /// The file is attributed to the tenant the caller is acting in, or is owned by the calling
    /// account, and may be served or removed.
    /// </summary>
    Granted = 1,

    /// <summary>
    /// No stored file is recorded under that name, so there is nothing to serve and nothing to say
    /// about who it might have belonged to.
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// The file exists but is attributed to another tenant, or is owned by another account. This is
    /// what a known or guessed stored file name yields instead of another tenant's content.
    /// </summary>
    CrossTenantAccess = 3,

    /// <summary>
    /// The file is attributed to a tenant that is suspended. Its content is retained and simply
    /// stops being served until the tenant is reactivated.
    /// </summary>
    TenantSuspended = 4,

    /// <summary>
    /// The file is attributed to a tenant that no longer exists or has been deleted. As with
    /// suspension, the content is retained and no longer served.
    /// </summary>
    TenantNotFound = 5
}

/// <summary>
/// Default <see cref="IFileService"/> implementation. It delegates the bytes to an
/// <see cref="IStorageProvider"/>, which knows nothing about tenants, and keeps the whole
/// attribution question here: an upload writes a <see cref="StoredFile"/> row naming the tenant or
/// the owning account, and a download or delete finds that row across every tenant first and then
/// decides whether this caller may have it. The physical layout of storage is deliberately left
/// flat, so swapping the provider changes nothing about who can read what.
/// </summary>
[NoDirectUse]
public class FileService(
    IStorageProvider storageProvider,
    AppDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService) : IFileService
{
    /// <summary>
    /// Generates a unique filename based on a GUID plus the original extension, records the file's
    /// attribution, and delegates persistence of the content to the storage provider.
    /// </summary>
    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, bool accountOwned = false, CancellationToken cancellationToken = default)
    {
        // Attribution is settled before anything is written: an upload that may not be attributed
        // fails here, with no orphaned bytes left in storage.
        var storedFile = new StoredFile
        {
            TenantId = accountOwned ? null : RequireActiveTenantId(),
            OwnerUserId = accountOwned ? RequireCurrentUserId() : null,
            FileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}",
            OriginalFileName = fileName,
            ContentType = contentType
        };

        await storageProvider.SaveAsync(stream, storedFile.FileName, contentType);

        try
        {
            await PersistAsync(storedFile, accountOwned, cancellationToken);
        }
        catch
        {
            // Left tracked as added, the record that just failed to save would be retried by the
            // next save on this context - and an account-owned one, saved outside platform scope
            // that time, would be stamped with the active tenant and quietly become its data. Drop
            // it first, so the failure ends here rather than resurfacing attributed to a tenant.
            dbContext.Entry(storedFile).State = EntityState.Detached;

            // The record is what grants access, so content stored without one is unreachable
            // forever. Remove it rather than leave it behind, then let the failure surface. The
            // removal is best-effort on purpose: the caller needs the reason the upload failed, and
            // a failure to tidy up after it must not take that reason's place.
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

        return storedFile.FileName;
    }

    /// <summary>
    /// Resolves the file's attribution and, when access is granted, retrieves its content stream
    /// and the content type recorded at upload time. A record whose content has gone missing from
    /// storage answers as a missing file rather than as an empty one.
    /// </summary>
    public async Task<FileDownloadResult> DownloadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var (status, storedFile) = await ResolveAsync(fileName, cancellationToken);
        if (storedFile is null)
        {
            return new FileDownloadResult { Status = status };
        }

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
    /// Resolves the file's attribution and, when access is granted, removes the record and then the
    /// content. Anything else is refused and left exactly as it was.
    /// </summary>
    public async Task<FileDeleteResult> DeleteAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var (status, storedFile) = await ResolveAsync(fileName, cancellationToken);
        if (storedFile is null)
        {
            return new FileDeleteResult { Status = status };
        }

        // The record goes first. While it exists the file is reachable, so a failure between the
        // two steps leaves a readable file rather than an attribution pointing at nothing.
        dbContext.StoredFiles.Remove(storedFile);
        await dbContext.SaveChangesAsync(cancellationToken);

        await storageProvider.DeleteAsync(storedFile.FileName);

        return new FileDeleteResult { Status = FileAccessStatus.Granted };
    }

    /// <summary>
    /// Finds the record for a stored file name and decides whether this caller, acting in the
    /// tenant they are acting in, may have it. The record is looked up across every tenant on
    /// purpose: the tenant query filter would turn another tenant's file into a missing one, and
    /// telling those two apart is the whole point of recording attribution.
    /// </summary>
    /// <param name="fileName">The stored file name the request supplied.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// The verdict, together with the record when - and only when - access is granted, so that no
    /// refused path can accidentally reach the file it was refused.
    /// </returns>
    /// <exception cref="TenantScopeNotEstablishedException">
    /// The file is tenant-scoped and no scope has been established to compare it against.
    /// </exception>
    private async Task<(FileAccessStatus Status, StoredFile? File)> ResolveAsync(string fileName, CancellationToken cancellationToken)
    {
        var storedFile = await dbContext.StoredFiles
            .AcrossAllTenants()
            .FirstOrDefaultAsync(f => f.FileName == fileName, cancellationToken);

        if (storedFile is null)
        {
            return (FileAccessStatus.NotFound, null);
        }

        // An account-owned file names no tenant, so it is judged against the account alone and
        // never against the active scope - which is exactly what lets its owner read it while
        // acting in any tenant, or in none at all.
        if (storedFile.TenantId is null)
        {
            return storedFile.OwnerUserId is { } ownerUserId && ownerUserId == currentUserService.GetCurrentUserId()
                ? (FileAccessStatus.Granted, storedFile)
                : (FileAccessStatus.CrossTenantAccess, null);
        }

        // The owning tenant's own state is read before the caller's, so a suspended or deleted
        // tenant's files stop being served to anybody while their content is retained. A deleted
        // tenant is a soft-deleted row, which this query does not see, so it answers as missing.
        var owningTenant = await dbContext.Tenants
            .Where(t => t.Id == storedFile.TenantId)
            .Select(t => new { t.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (owningTenant is null)
        {
            return (FileAccessStatus.TenantNotFound, null);
        }

        if (owningTenant.Status != TenantStatus.Active)
        {
            return (FileAccessStatus.TenantSuspended, null);
        }

        return storedFile.TenantId == tenantContext.CurrentTenantId
            ? (FileAccessStatus.Granted, storedFile)
            : (FileAccessStatus.CrossTenantAccess, null);
    }

    /// <summary>
    /// Writes the attribution record, opening platform scope for an account-owned file.
    /// </summary>
    /// <param name="storedFile">The record to persist.</param>
    /// <param name="accountOwned">Whether the file belongs to an account rather than to a tenant.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <remarks>
    /// The scope is needed because save-time attribution stamps the active tenant onto a
    /// tenant-scoped row that names none: without it, a profile image uploaded while acting in a
    /// tenant would quietly become that tenant's data and vanish when its owner switched tenants.
    /// It covers the save rather than only the add, since the stamping happens at save time, so a
    /// caller must not be holding unsaved additions of other tenant-scoped rows in the same
    /// <see cref="AppDbContext"/> when it uploads an account-owned file: those rows would be
    /// flushed here in platform scope and stamped with no tenant. Save that work first.
    /// </remarks>
    private async Task PersistAsync(StoredFile storedFile, bool accountOwned, CancellationToken cancellationToken)
    {
        using IDisposable? platformScope = accountOwned ? tenantContext.BeginPlatformScope() : null;

        dbContext.StoredFiles.Add(storedFile);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the tenant a tenant-scoped upload is attributed to. Platform scope is refused rather
    /// than read as "no tenant": a file that belongs to no tenant and to no account belongs to
    /// nobody, and nothing would ever be allowed to read it again.
    /// </summary>
    /// <returns>The identifier of the tenant currently being acted for.</returns>
    /// <exception cref="TenantScopeNotEstablishedException">No tenant scope has been established.</exception>
    /// <exception cref="InvalidOperationException">The active scope is platform scope rather than a tenant.</exception>
    private Guid RequireActiveTenantId()
        => tenantContext.CurrentTenantId
           ?? throw new InvalidOperationException(
               "A tenant-scoped file must be uploaded while acting in a tenant. Upload it as account-owned to attribute it to the calling account instead.");

    /// <summary>
    /// Returns the account an account-owned upload belongs to, refusing an unauthenticated caller:
    /// there would be no account for the file to belong to, and therefore no one able to read it.
    /// </summary>
    /// <returns>The identifier of the account making the request.</returns>
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
/// Result of a download attempt, bundling the verdict with the file's content stream and its MIME
/// type. Content is present only when <see cref="Status"/> is
/// <see cref="FileAccessStatus.Granted"/>; every other status carries the reason and nothing else,
/// so a refused download has nothing to stream by construction.
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