namespace Backend.Base.Dto;

/// <summary>
/// Implemented by DTOs that report whether their row was created by the system itself rather than
/// by a user. It is the payload counterpart of
/// <see cref="Backend.ShareData.Entities.Base.ISystemCreated"/>, and it exists so a client can tell
/// in advance that an update or a delete will be refused, and present the row accordingly instead of
/// discovering the refusal by attempting the call.
/// </summary>
public interface ISystemCreatedDto
{
    bool SystemCreated { get; set; }
}
