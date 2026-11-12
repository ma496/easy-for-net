namespace Backend.Base.Dto;

/// <summary>
/// Marker interface for DTOs that carry both creation and update audit metadata.
/// </summary>
public interface IAuditableDto : ICreatableDto, IUpdatableDto
{
}

/// <summary>
/// Interface for DTOs that expose creation audit information (created timestamp and creator id).
/// </summary>
public interface ICreatableDto
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
}

/// <summary>
/// Interface for DTOs that expose update audit information (last updated timestamp and updater id).
/// </summary>
public interface IUpdatableDto
{
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}