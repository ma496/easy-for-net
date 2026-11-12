namespace Backend.ShareData.Entities.Base;

/// <summary>
/// Implemented by entities that participate in soft deletion, where rows are
/// flagged as deleted rather than physically removed from the database.
/// </summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
