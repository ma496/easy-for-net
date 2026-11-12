namespace Backend.Base.Dto;

/// <summary>
/// Implemented by DTOs that report the tenant their row belongs to, for a kind whose rows belong to
/// at most one tenant. It is the payload counterpart of
/// <see cref="Backend.ShareData.Entities.Base.IMayHaveTenant"/>: a null <see cref="TenantId"/> is
/// platform scope - a row that is not tenant data - which is why this contract is the one to choose
/// when a kind has rows that legitimately belong to no tenant. When every row names a tenant,
/// implement <see cref="IHaveTenantDto"/> instead.
/// </summary>
public interface IMayHaveTenantDto
{
    Guid? TenantId { get; set; }
}
