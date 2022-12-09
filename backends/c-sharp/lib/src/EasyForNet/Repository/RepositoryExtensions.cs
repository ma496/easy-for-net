using System.Linq.Expressions;
using Ardalis.GuardClauses;
using JetBrains.Annotations;
using System.Linq.Dynamic.Core;
using EasyForNet.Application.Services.Crud;

namespace System.Linq;

public static class RepositoryExtensions
{
    public static IQueryable<T> PageBy<T>([NotNull] this IQueryable<T> query, IPagedResultRequest pagedResultRequest)
    {
        Guard.Against.Null(query, nameof(query));

        return query.PageBy(pagedResultRequest.SkipCount, pagedResultRequest.MaxResultCount);
    }

    public static IQueryable<T> PageBy<T>([NotNull] this IQueryable<T> query, int skipCount, int maxResultCount)
    {
        Guard.Against.Null(query, nameof(query));

        return query.Skip(skipCount).Take(maxResultCount);
    }

    public static TQueryable PageBy<T, TQueryable>([NotNull] this TQueryable query, int skipCount, int maxResultCount)
        where TQueryable : IQueryable<T>
    {
        Guard.Against.Null(query, nameof(query));

        return (TQueryable)query.Skip(skipCount).Take(maxResultCount);
    }

    public static IQueryable<T> WhereIf<T>([NotNull] this IQueryable<T> query, bool condition, Expression<Func<T, bool>> predicate)
    {
        Guard.Against.Null(query, nameof(query));

        return condition
            ? query.Where(predicate)
            : query;
    }

    public static TQueryable WhereIf<T, TQueryable>([NotNull] this TQueryable query, bool condition, Expression<Func<T, bool>> predicate)
        where TQueryable : IQueryable<T>
    {
        Guard.Against.Null(query, nameof(query));

        return condition
            ? (TQueryable)query.Where(predicate)
            : query;
    }

    public static IQueryable<T> WhereIf<T>([NotNull] this IQueryable<T> query, bool condition, Expression<Func<T, int, bool>> predicate)
    {
        Guard.Against.Null(query, nameof(query));

        return condition
            ? query.Where(predicate)
            : query;
    }

    public static TQueryable WhereIf<T, TQueryable>([NotNull] this TQueryable query, bool condition, Expression<Func<T, int, bool>> predicate)
        where TQueryable : IQueryable<T>
    {
        Guard.Against.Null(query, nameof(query));

        return condition
            ? (TQueryable)query.Where(predicate)
            : query;
    }

    public static TQueryable OrderByIf<T, TQueryable>([NotNull] this TQueryable query, bool condition, string sorting)
        where TQueryable : IQueryable<T>
    {
        Guard.Against.Null(query, nameof(query));

        return condition
            ? (TQueryable)query.OrderBy(sorting)
            : query;
    }
}
