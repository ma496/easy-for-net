using Backend.ShareData.Entities.Base;

namespace Backend.Features.FileManagement.Core.Entities;

/// <summary>
/// Persisted record of one uploaded file. An application file carries no owner and any signed-in
/// caller may read it; an account-owned file, such as a profile image, names the account that owns
/// it and is read back by that account alone.
/// </summary>
public class StoredFile : AuditableEntity<Guid>
{
    // The account an account-owned file belongs to. Null means an application file; the uploader of
    // either kind is already recorded in CreatedBy.
    public Guid? OwnerUserId { get; set; }

    // The generated name the file is stored under, and the storage-level identity of the blob.
    // Knowing it is not enough to read an account-owned file: the owner above still decides access.
    public string FileName { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
}
