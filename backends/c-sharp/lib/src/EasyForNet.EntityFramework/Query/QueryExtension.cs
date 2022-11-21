using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Text;
using System.Text.RegularExpressions;
using Ardalis.GuardClauses;

namespace EasyForNet.EntityFramework.Query;

public static class QueryExtension
{
    public static IQueryable<TEntity> CuQuery<TEntity>(this IQueryable<TEntity> query, IQueryParams queryParams)
        where TEntity : class
    {
        Guard.Against.Null(queryParams, nameof(queryParams));

        return query.SortAndFilterQuery(queryParams)
            .PagingQuery(queryParams);
    }

    public static IQueryable<TEntity> SortAndFilterQuery<TEntity>(this IQueryable<TEntity> query,
        ISortAndFilterQueryParams sortAndFilterQueryParams)
        where TEntity : class
    {
        Guard.Against.Null(sortAndFilterQueryParams, nameof(sortAndFilterQueryParams));

        var queryable = query;
        if (!string.IsNullOrWhiteSpace(sortAndFilterQueryParams.Filter))
        {
            var (predicate, args) = ParseFilter(sortAndFilterQueryParams.Filter);
            queryable = queryable.Where(predicate, args);
        }

        if (!string.IsNullOrWhiteSpace(sortAndFilterQueryParams.SortBy))
            queryable = queryable.OrderBy(sortAndFilterQueryParams.SortBy);

        return queryable;
    }

    public static IQueryable<TEntity> PagingQuery<TEntity>(this IQueryable<TEntity> query,
        IPagingQueryParams pagingQueryParams)
        where TEntity : class
    {
        Guard.Against.Null(pagingQueryParams, nameof(pagingQueryParams));

        if (pagingQueryParams.Page < 1)
            throw new ArgumentOutOfRangeException(
                $"{nameof(pagingQueryParams.Page)} value must be greater then zero");
        if (pagingQueryParams.PageSize < 1)
            throw new ArgumentOutOfRangeException(
                $"{nameof(pagingQueryParams.PageSize)} value must be greater then zero");

        var queryable = query;
        var skip = (pagingQueryParams.Page - 1) * pagingQueryParams.PageSize;
        queryable = queryable.Skip(skip)
            .Take(pagingQueryParams.PageSize);

        return queryable;
    }

    private static (string, object[]) ParseFilter(string filter)
    {
        var predicate = new StringBuilder(filter);
        var args = new List<object>();

        var matchCollection = Regex.Matches(filter, @"(\s+eq\s+|\s+ne\s+|\s+gt\s+|\s+ge\s+|\s+lt\s+|\s+le\s+)");
        for (var i = 0; i < matchCollection.Count; i++)
        {
            var match = matchCollection[i];
            var position = match.Index + match.Length;
            var subStr = filter.Substring(position);
            var matchSubStr = Regex.Match(subStr, @"(\s+and\s+|\s+or\s+)");
            var value = matchSubStr.Success ? subStr.Substring(0, matchSubStr.Index) : subStr;
            predicate.Replace(value, $"@{i}", position - (filter.Length - predicate.Length), value.Length);
            args.Add(value);
        }

        var otherMatchCollection = Regex.Matches(predicate.ToString(),
            @"(\s+eq\s+|\s+ne\s+|\s+gt\s+|\s+ge\s+|\s+lt\s+|\s+le\s+)|\s+and\s+|\s+or\s+");
        foreach (Match match in otherMatchCollection)
            if (Regex.IsMatch(match.Value, @"\s+eq\s+"))
                predicate.Replace("eq", "==", match.Index, match.Length);
            else if (Regex.IsMatch(match.Value, @"\s+ne\s+"))
                predicate.Replace("ne", "!=", match.Index, match.Length);
            else if (Regex.IsMatch(match.Value, @"\s+gt\s+"))
                predicate.Replace("gt", "> ", match.Index, match.Length);
            else if (Regex.IsMatch(match.Value, @"\s+ge\s+"))
                predicate.Replace("ge", ">=", match.Index, match.Length);
            else if (Regex.IsMatch(match.Value, @"\s+lt\s+"))
                predicate.Replace("lt", "< ", match.Index, match.Length);
            else if (Regex.IsMatch(match.Value, @"\s+le\s+"))
                predicate.Replace("le", "<=", match.Index, match.Length);
            else if (Regex.IsMatch(match.Value, @"\s+and\s+"))
                predicate.Replace("and", "&& ", match.Index, match.Length);
            else if (Regex.IsMatch(match.Value, @"\s+or\s+"))
                predicate.Replace("or", "||", match.Index, match.Length);
            else
                throw new NotImplementedException($"No handler for {match.Value}");

        return (predicate.ToString(), args.ToArray());
    }
}