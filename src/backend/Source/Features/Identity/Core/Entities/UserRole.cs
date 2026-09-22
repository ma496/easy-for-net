namespace Backend.Features.Identity.Core.Entities;

using Backend.ShareData.Entities.Base;

/// <summary>
/// Junction entity linking a <see cref="User"/> to a <see cref="Role"/>, representing the user's role memberships.
/// </summary>
public class UserRole : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}