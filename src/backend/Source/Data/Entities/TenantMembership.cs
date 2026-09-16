namespace Backend.Data.Entities;

using Backend.Data.Entities.Base;

/// <summary>
/// The link that makes a user account a member of a tenant. A membership carries no state of its
/// own: it exists, or it has been removed. The roles the member holds inside the tenant are the
/// user's role assignments whose role belongs to that tenant. <see cref="TenantId"/> is never null
/// here - <c>TenantMembershipConfiguration</c> makes the column required - and that same
/// configuration maps PostgreSQL's <c>xmin</c> system column as a shadow concurrency token, so of
/// two concurrent replacements of a member's role assignments the losing writer fails instead of
/// overwriting a set it never saw, leaving the entity itself clean.
/// </summary>
public class TenantMembership : AuditableEntity<Guid>, ISoftDelete, ITenantScoped
{
    public Guid? TenantId { get; set; }
    public Guid UserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}