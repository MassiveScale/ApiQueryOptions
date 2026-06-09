using ApiQueryOptions;
using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Extensions;
using ApiQueryOptions.Options;
using Microsoft.EntityFrameworkCore;

namespace ApiQueryOptions.EntityFrameworkCore.Extensions;

/// <summary>
/// EF Core-specific IQueryable&lt;T&gt; extension methods.
/// All methods compose deferred expression trees; nothing is enumerated until
/// the caller calls <c>.ToListAsync()</c> or similar.
/// </summary>
public static class EFQueryableExtensions
{
    /// <summary>
    /// Applies all non-null options from <paramref name="options"/> to <paramref name="query"/>:
    /// Filter → OrderBy → Skip → Top → Expand (Include).
    /// The entire pipeline — WHERE, ORDER BY, OFFSET, LIMIT, and JOINs — is sent as a single SQL round-trip.
    /// </summary>
    public static IQueryable<T> Apply<T>(this IQueryable<T> query, ApiQueryOptions<T> options)
        where T : class
    {
        // Apply core options (filter / orderby / skip / top) via the core extension
        query = IQueryableExtensions.Apply(query, options);

        // Apply EF Core expand (Include / JOIN)
        if (options.Expand is not null)
        {
            query = query.ApplyExpand(options.Expand, options.Settings);
        }

        return query;
    }

    /// <summary>
    /// Applies <c>$expand</c> navigation property paths as EF Core <c>.Include()</c> calls.
    /// Supports dot-notation for nested paths (e.g. <c>Orders.Items</c>).
    /// </summary>
    /// <exception cref="QueryOptionDisabledException">When <c>settings.ExpandEnabled</c> is <c>false</c>.</exception>
    public static IQueryable<T> ApplyExpand<T>(
        this IQueryable<T> query,
        ExpandQueryOption expand,
        ApiQueryOptionsSettings settings)
        where T : class
    {
        if (!settings.ExpandEnabled)
        {
            throw new QueryOptionDisabledException("expand");
        }

        foreach (string path in expand.NavigationProperties)
        {
            query = query.Include(path);
        }

        return query;
    }
}