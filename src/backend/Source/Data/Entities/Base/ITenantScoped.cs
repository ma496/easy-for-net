namespace Backend.Data.Entities.Base;

/// <summary>
/// Implemented by entities whose rows belong to exactly one tenant. <see cref="AppDbContext"/>
/// registers the named "Tenant" query filter for every implementing type, attributes new rows to
/// the active tenant on save, and refuses to persist a row that carries no attribution.
/// </summary>
public interface ITenantScoped
{
    Guid? TenantId { get; set; }
}