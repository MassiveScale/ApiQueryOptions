using ApiQueryOptions;
using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Options;
using Microsoft.EntityFrameworkCore;

namespace ApiQueryOptions.EntityFrameworkCore.Extensions;

/// <summary>
/// EF Core-specific IQueryable&lt;T&gt; extension methods.
/// All methods compose deferred expression trees; nothing is enumerated until
/// the caller calls <c>.ToListAsync()</c> or similar.
/// The core <c>IQueryableExtensions.Apply</c> calls <see cref="ApplyExpand{T}"/> automatically when
/// this package is installed, so most callers never need to import this namespace.
/// </summary>
public static class EFQueryableExtensions
{
    /// <summary>
    /// Applies <c>$expand</c> navigation property paths as EF Core <c>.Include()</c> calls.
    /// Supports dot-notation for nested paths (e.g. <c>Orders.Items</c>).
    /// </summary>
    /// <remarks>
    /// The core package locates this method by its name and signature at run time. Renaming it,
    /// changing its parameters, or adding another generic overload with three parameters stops
    /// <c>Apply</c> from expanding.
    /// </remarks>
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
