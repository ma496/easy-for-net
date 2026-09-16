namespace Backend.Exceptions;

/// <summary>
/// Exception thrown when a tenant-scoped read or write is reached with no tenant scope established
/// and no explicit opt-out. Unresolved is deliberately distinct from platform scope: work that runs
/// outside a user request, such as a scheduled or queued job, must name the tenant it acts for, so
/// reaching this point is a programming error rather than a user error. Failing here is what keeps
/// such a caller from silently reading every tenant's rows, reading none, or persisting a row with
/// no tenant attribution.
/// </summary>
public sealed class TenantScopeNotEstablishedException(
    string message = "No tenant scope has been established for the current operation.")
    : Exception(message)
{
}