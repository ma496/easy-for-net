namespace Backend.ShareData.Entities.Base;

/// <summary>
/// Implemented by entities whose rows can be created by the system itself rather than by a user -
/// the seeded administrator account, its role, the bootstrap tenant. A row flagged
/// <see cref="SystemCreated"/> is part of the installation's own fabric, so endpoints refuse to
/// update or delete it and report the refusal with the matching error code.
/// </summary>
public interface ISystemCreated
{
    bool SystemCreated { get; set; }
}
