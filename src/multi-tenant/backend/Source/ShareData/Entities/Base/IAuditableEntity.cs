namespace Backend.ShareData.Entities.Base;

/// <summary>
/// Interface for entities that carry both creation and update audit metadata.
/// </summary>
public interface IAuditableEntity : ICreatableEntity, IUpdatableEntity
{
}

/// <summary>
/// Interface for entities that expose creation audit information (created timestamp and creator id).
/// </summary>
public interface ICreatableEntity
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
}

/// <summary>
/// Interface for entities that expose update audit information (last updated timestamp and updater id).
/// </summary>
public interface IUpdatableEntity
{
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}