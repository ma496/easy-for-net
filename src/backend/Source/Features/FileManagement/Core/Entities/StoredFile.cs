namespace Backend.Features.FileManagement.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// Persisted record of one uploaded file. A tenant-scoped file carries the tenant it was uploaded
/// in; an account-owned file, such as a profile image, carries no tenant and instead names the
/// account that owns it.
/// </summary>
public class StoredFile : AuditableEntity<Guid>, IMayHaveTenant
{
    // The tenant the file was uploaded in. Null means the file is not tenant data:
    // together with OwnerUserId it marks an account-owned file, such as a profile image,
    // which its owner can read while acting in any tenant or in none.
    public Guid? TenantId { get; set; }

    // The account an account-owned file belongs to. A null tenant together with an owner is what
    // makes a file account-owned; tenant data is identified by its tenant, and its uploader is
    // already recorded in CreatedBy.
    public Guid? OwnerUserId { get; set; }

    // The generated name the file is stored under, and the storage-level identity of the blob.
    // Knowing it is not enough to read the file: attribution above still decides access.
    public string FileName { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
}