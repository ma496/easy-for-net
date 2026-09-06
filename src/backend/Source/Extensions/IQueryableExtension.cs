namespace Backend.Extensions;

using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Backend.Data.Entities.Base;

/// <summary>
/// Queryable extensions that apply the standard pagination, sorting, and
/// inclusion rules shared by the list endpoints.
/// </summary>
public static class IQueryableExtension
{
    /// <summary>
    /// Applies the sort, include, and pagination rules described by a
    /// <see cref="ListRequestDto{TId}"/> to the source query, choosing a
    /// sensible default ordering based on the entity's audit metadata.
    /// </summary>
    /// <param name="query">The source queryable of <see cref="IBaseEntity{TId}"/> instances.</param>
    /// <param name="request">The list request describing sorting and pagination.</param>
    /// <param name="applyDefaultOrdering">Whether to apply default ordering by CreatedAt or UpdatedAt when no explicit sort field is provided; defaults to true.</param>
    /// <returns>A new <see cref="IQueryable{T}"/> with the requested ordering, filter, and paging applied.</returns>
    public static IQueryable<T> Process<T, TId>(this IQueryable<T> query, ListRequestDto<TId> request,
        bool applyDefaultOrdering = true)
        where T : class, IBaseEntity<TId>
    {
        var processQuery = query;

        if (!string.IsNullOrWhiteSpace(request.SortField))
        {
            var sortProperty = typeof(T).GetProperties()
                .FirstOrDefault(property => string.Equals(property.Name, request.SortField, StringComparison.OrdinalIgnoreCase));
            if (sortProperty is null || !IsSortableType(sortProperty.PropertyType))
            {
                throw new ArgumentException($"'{request.SortField}' is not a sortable field.", nameof(request.SortField));
            }

            processQuery = processQuery.OrderBy($"{sortProperty.Name} {(request.SortDirection == SortDirection.Desc ? "DESC" : "ASC")}");
        }
        else if (applyDefaultOrdering && typeof(IUpdatableEntity).IsAssignableFrom(typeof(T)))
        {
            processQuery = processQuery.OrderBy($"{nameof(IUpdatableEntity.UpdatedAt)} DESC");
        }
        else if (applyDefaultOrdering && typeof(ICreatableEntity).IsAssignableFrom(typeof(T)))
        {
            processQuery = processQuery.OrderBy($"{nameof(ICreatableEntity.CreatedAt)} DESC");
        }

        if (request.IncludeIds?.Count > 0)
        {
            processQuery = processQuery.Where(x => request.IncludeIds.Contains(x.Id));
        }

        processQuery = request.All
            ? processQuery.Take<T>(10000)
            : request.IncludeIds?.Count > 0
                ? processQuery
                : processQuery
                    .Skip<T>((request.Page - 1) * request.PageSize)
                    .Take<T>(request.PageSize);

        return processQuery;
    }

    private static bool IsSortableType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid)
               || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(decimal);
    }

    /// <summary>
    /// Applies <paramref name="predicate"/> to the query only when
    /// <paramref name="condition"/> is <c>true</c>; otherwise returns the query unchanged.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="condition">Whether to apply the predicate.</param>
    /// <param name="predicate">The filter to apply when the condition holds.</param>
    /// <returns>The filtered or unchanged queryable.</returns>
    public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool condition, Expression<Func<T, bool>> predicate)
    {
        return condition ? query.Where(predicate) : query;
    }

    /// <summary>
    /// Orders the query by the supplied expression only when
    /// <paramref name="condition"/> is <c>true</c>; otherwise returns the query unchanged.
    /// </summary>
    /// <param name="query">The source queryable.</param>
    /// <param name="condition">Whether to apply the ordering.</param>
    /// <param name="expression">The key selector to order by when the condition holds.</param>
    /// <param name="direction">The sort direction to use; defaults to ascending.</param>
    /// <returns>The ordered or unchanged queryable.</returns>
    public static IQueryable<T> OrderIf<T>(this IQueryable<T> query, bool condition, Expression<Func<T, object>> expression, SortDirection direction = SortDirection.Asc)
    {
        if (!condition)
        {
            return query;
        }

        return direction == SortDirection.Desc
            ? query.OrderByDescending(expression)
            : query.OrderBy(expression);
    }
}
