namespace Backend.Base.Dto;

/// <summary>
/// Implemented by DTOs that report the tenant their row belongs to, for a kind whose rows always
/// belong to exactly one tenant. It is the payload counterpart of
/// <see cref="Backend.ShareData.Entities.Base.IHaveTenant"/> and the strict counterpart of
/// <see cref="IMayHaveTenantDto"/>: because <see cref="TenantId"/> is not nullable there is no
/// platform-scoped row for a client to represent or to handle.
/// </summary>
public interface IHaveTenantDto
{
    Guid TenantId { get; set; }
}
