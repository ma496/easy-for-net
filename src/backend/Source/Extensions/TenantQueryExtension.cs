namespace Backend.Extensions;

/// <summary>
/// Query extensions that relax tenant restriction explicitly and by name. This is the only
/// sanctioned way to read across tenants; the soft-delete filter stays in force.
/// </summary>
public static class TenantQueryExtension
{
    /// <summary>
    /// Suppresses the named <c>Tenant</c> query filter for this query only, leaving every other
    /// filter - including the soft-delete filter - applied, so a deliberate cross-tenant read
    /// still excludes soft-deleted rows.
    /// </summary>
    /// <typeparam name="T">The entity type being queried.</typeparam>
    /// <param name="query">The source query whose tenant restriction is relaxed.</param>
    /// <returns>A new <see cref="IQueryable{T}"/> that spans every tenant.</returns>
    /// <remarks>
    /// Entity types that carry no <c>Tenant</c> filter - the ones exempt from tenant restriction -
    /// are unaffected: naming a filter the entity type does not have is a no-op, never an error.
    /// </remarks>
    public static IQueryable<T> AcrossAllTenants<T>(this IQueryable<T> query) where T : class
        => query.IgnoreQueryFilters(["Tenant"]);
}