namespace Backend.Exceptions;

/// <summary>
/// Exception thrown when a tenant-scoped record would be persisted with a tenant attribution other
/// than the active one - a new record carrying a foreign tenant, or an existing record whose tenant
/// is being changed. Attribution is stamped from the active scope and is never taken from the caller,
/// so reaching this point is a programming error rather than a user error; a request that merely
/// reaches another tenant's record is refused earlier, as if that record did not exist.
/// </summary>
public sealed class TenantAttributionException(
    string message = "The record's tenant attribution does not match the active tenant.")
    : Exception(message)
{
}