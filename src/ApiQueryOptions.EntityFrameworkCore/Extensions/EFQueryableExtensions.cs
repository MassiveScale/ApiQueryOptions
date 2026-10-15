using System.Reflection;
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
    /// Supports dot-notation for nested paths (e.g. <c>Orders.Items</c>), where a segment after a
    /// collection refers to a property of the collection's element type. Property names are
    /// matched case-insensitively and passed to <c>.Include()</c> with their declared casing.
    /// </summary>
    /// <remarks>
    /// The core package locates this method by its name and signature at run time. Renaming it,
    /// changing its parameters, or adding another generic overload with three parameters stops
    /// <c>Apply</c> from expanding.
    /// </remarks>
    /// <exception cref="QueryOptionDisabledException">When <c>settings.ExpandEnabled</c> is <c>false</c>.</exception>
    /// <exception cref="InvalidQueryOptionException">When a path segment is not a public property.</exception>
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
            query = query.Include(ResolveNavigationPath(typeof(T), path));
        }

        return query;
    }

    /// <summary>
    /// Validates each segment of <paramref name="path"/> against <paramref name="rootType"/> and
    /// returns the path with every segment in its declared casing.
    /// </summary>
    /// <exception cref="InvalidQueryOptionException">When a segment is not a public property.</exception>
    internal static string ResolveNavigationPath(Type rootType, string path)
    {
        string[] segments = path.Split('.');
        Type current = rootType;

        for (int i = 0; i < segments.Length; i++)
        {
            PropertyInfo property = FindProperty(current, segments[i])
                ?? throw new InvalidQueryOptionException(
                    "expand",
                    path,
                    $"Property '{segments[i]}' not found on type '{current.Name}'.");

            segments[i] = property.Name;
            current = GetNavigationTargetType(property.PropertyType);
        }

        return string.Join('.', segments);
    }

    private static PropertyInfo? FindProperty(Type type, string name)
    {
        PropertyInfo? caseInsensitiveMatch = null;

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || property.GetGetMethod() is null)
            {
                continue;
            }

            if (string.Equals(property.Name, name, StringComparison.Ordinal))
            {
                return property;
            }

            if (caseInsensitiveMatch is null && string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                caseInsensitiveMatch = property;
            }
        }

        return caseInsensitiveMatch;
    }

    /// <summary>
    /// Returns the element type when <paramref name="type"/> is a collection (other than
    /// <see cref="string"/>), otherwise <paramref name="type"/> itself.
    /// </summary>
    private static Type GetNavigationTargetType(Type type)
    {
        if (type == typeof(string))
        {
            return type;
        }

        if (type.IsArray)
        {
            return type.GetElementType()!;
        }

        Type? enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0] ?? type;
    }
}