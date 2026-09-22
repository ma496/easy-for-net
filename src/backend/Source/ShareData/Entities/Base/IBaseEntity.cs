namespace Backend.ShareData.Entities.Base;

/// <summary>
/// Root interface for all entities in the system.
/// </summary>
public interface IBaseEntity
{}

/// <summary>
/// Base entity interface exposing a strongly-typed identifier.
/// </summary>
public interface IBaseEntity<TId> : IBaseEntity
{
    TId Id { get; set; }
}