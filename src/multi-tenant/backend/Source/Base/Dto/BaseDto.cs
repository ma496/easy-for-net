namespace Backend.Base.Dto;

/// <summary>
/// Minimal base DTO exposing only a strongly-typed identifier.
/// </summary>
public abstract class BaseDto<TId> : IBaseDto<TId>
{
    public TId Id { get; set; } = default!;
}