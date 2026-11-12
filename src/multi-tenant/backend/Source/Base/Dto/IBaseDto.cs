namespace Backend.Base.Dto;

/// <summary>
/// Root marker interface for all DTOs in the system.
/// </summary>
public interface IBaseDto
{}

/// <summary>
/// Base DTO interface exposing a strongly-typed identifier.
/// </summary>
public interface IBaseDto<TId> : IBaseDto
{
    TId Id { get; set; }
}