namespace Backend.Base.Dto;

/// <summary>
/// Base DTO for entities that track both creation and update audit information
/// and expose a strongly-typed identifier.
/// </summary>
public abstract class AuditableDto<TId> : AuditableDto, IBaseDto<TId>
{
    public TId Id { get; set; } = default!;
}

/// <summary>
/// Base DTO for entities that track both creation and update audit information.
/// </summary>
public abstract class AuditableDto : IAuditableDto, IBaseDto
{
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Base DTO for entities that track creation audit information and expose
/// a strongly-typed identifier.
/// </summary>
public abstract class CreatableDto<TId> : CreatableDto, IBaseDto<TId>
{
    public TId Id { get; set; } = default!;
}

/// <summary>
/// Base DTO for entities that track creation audit information.
/// </summary>
public abstract class CreatableDto : ICreatableDto, IBaseDto
{
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}

/// <summary>
/// Base DTO for entities that track update audit information and expose
/// a strongly-typed identifier.
/// </summary>
public abstract class UpdatableDto<TId> : UpdatableDto, IBaseDto<TId>
{
    public TId Id { get; set; } = default!;
}

/// <summary>
/// Base DTO for entities that track update audit information.
/// </summary>
public abstract class UpdatableDto : IUpdatableDto, IBaseDto
{
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}