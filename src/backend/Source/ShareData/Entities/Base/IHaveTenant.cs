namespace Backend.ShareData.Entities.Base;

/// <summary>
/// Implemented by entities whose rows always belong to exactly one tenant. It is the strict
/// counterpart of <see cref="IMayHaveTenant"/>: <see cref="AppDbContext"/> gives an implementing
/// type the same named "Tenant" query filter and the same attribution rules, but because
/// <see cref="TenantId"/> is not nullable there is no platform-scoped row to represent or to
/// handle. A row left unattributed is therefore rejected outright - including in platform scope,
/// where there is no tenant to stamp it with - rather than being written with no tenant.
/// </summary>
public interface IHaveTenant
{
    Guid TenantId { get; set; }
}
