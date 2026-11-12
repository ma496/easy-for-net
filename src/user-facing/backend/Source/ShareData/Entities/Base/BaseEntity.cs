namespace Backend.ShareData.Entities.Base;

/// <summary>
/// Minimal base entity implementation carrying no additional state beyond the
/// common marker contract.
/// </summary>
public abstract class BaseEntity : IBaseEntity
{
}

/// <summary>
/// Minimal base entity implementation exposing a strongly-typed identifier.
/// </summary>
public abstract class BaseEntity<TId> : IBaseEntity<TId>
{
    public TId Id { get; set; } = default!;
}