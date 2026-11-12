namespace Backend.ShareData.Entities.Base;

/// <summary>
/// Base entity that tracks both creation and update audit metadata and
/// exposes a strongly-typed identifier.
/// </summary>
public abstract class AuditableEntity<TId> : AuditableEntity, IBaseEntity<TId>
{
    public TId Id { get; set; } = default!;
}

/// <summary>
/// Base entity that tracks both creation and update audit metadata.
/// </summary>
public abstract class AuditableEntity : IAuditableEntity, IBaseEntity
{
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Base entity that tracks creation audit metadata and exposes a
/// strongly-typed identifier.
/// </summary>
public abstract class CreatableEntity<TId> : CreatableEntity, IBaseEntity<TId>
{
    public TId Id { get; set; } = default!;
}

/// <summary>
/// Base entity that tracks creation audit metadata.
/// </summary>
public abstract class CreatableEntity : ICreatableEntity, IBaseEntity
{
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}

/// <summary>
/// Base entity that tracks update audit metadata and exposes a
/// strongly-typed identifier.
/// </summary>
public abstract class UpdatableEntity<TId> : UpdatableEntity, IBaseEntity<TId>
{
    public TId Id { get; set; } = default!;
}

/// <summary>
/// Base entity that tracks update audit metadata.
/// </summary>
public abstract class UpdatableEntity : IUpdatableEntity, IBaseEntity
{
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}