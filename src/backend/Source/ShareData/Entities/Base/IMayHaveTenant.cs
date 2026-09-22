namespace Backend.ShareData.Entities.Base;

/// <summary>
/// Implemented by entities whose rows belong to at most one tenant. <see cref="AppDbContext"/>
/// registers the named "Tenant" query filter for every implementing type, attributes new rows to
/// the active tenant on save, and refuses to persist a row attributed to any other tenant. A null
/// <see cref="TenantId"/> is platform scope - a row that is not tenant data - which is why this
/// marker is the one to choose when a kind has rows that legitimately belong to no tenant. When
/// every row must name a tenant, implement <see cref="IHaveTenant"/> instead, so that the absence
/// of attribution is a compile-time impossibility rather than a state to guard against.
/// </summary>
public interface IMayHaveTenant
{
    Guid? TenantId { get; set; }
}
