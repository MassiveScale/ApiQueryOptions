using System.Linq.Expressions;
using System.Reflection;
using ApiQueryOptions;
using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Extensions;
using ApiQueryOptions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace ApiQueryOptions.EntityFrameworkCore.Extensions;

/// <summary>
/// EF Core-specific IQueryable&lt;T&gt; extension methods.
/// <see cref="ApplyExpand{T}"/> composes a deferred expression tree; nothing is enumerated until
/// the caller calls <c>.ToListAsync()</c> or similar. <see cref="CountAsync{T}"/> runs a count query.
/// The core <c>IQueryableExtensions.Apply</c> calls <see cref="ApplyExpand{T}"/> automatically when
/// this package is installed, so most callers never need to import this namespace.
/// </summary>
public static class EFQueryableExtensions
{
    /// <summary>
    /// Applies <c>$expand</c> navigation property paths as EF Core <c>.Include()</c> calls when
    /// <typeparamref name="T"/> is an entity type in the query's EF Core model. Supports dot-notation
    /// for nested paths (e.g. <c>Orders.Items</c>). Names are matched case-insensitively and passed
    /// to <c>.Include()</c> with their declared casing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On an entity query, every path segment must be a navigation in the model, otherwise an
    /// <see cref="InvalidQueryOptionException"/> is thrown.
    /// </para>
    /// <para>
    /// When the query has been projected to a type that is not an entity type (for example
    /// <c>db.Members.Select(m =&gt; new MemberDto { ... })</c>), or is not an EF Core query at all,
    /// nothing is included: the caller handles <c>$expand</c> in its own projection, typically with
    /// <see cref="ExpandQueryOption.Includes(string)"/>. Each path must still name a public
    /// property of <typeparamref name="T"/>, where a segment after a collection refers to a property
    /// of the collection's element type.
    /// </para>
    /// <para>
    /// The core package locates this method by its name and signature at run time. Renaming it,
    /// changing its parameters, or adding another generic overload with three parameters stops
    /// <c>Apply</c> from expanding.
    /// </para>
    /// </remarks>
    /// <exception cref="QueryOptionDisabledException">When <c>settings.ExpandEnabled</c> is <c>false</c>.</exception>
    /// <exception cref="InvalidQueryOptionException">
    /// When a path segment is not a navigation of an entity type, or, on a projected or non-EF query,
    /// not a public property.
    /// </exception>
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

        if (expand.NavigationProperties.Count == 0)
        {
            return query;
        }

        IEntityType? entityType = FindEntityType(query);

        foreach (string path in expand.NavigationProperties)
        {
            if (entityType is null)
            {
                ResolveNavigationPath(typeof(T), path);
            }
            else
            {
                query = query.Include(ResolveNavigationPath(entityType, path));
            }
        }

        return query;
    }

    /// <summary>
    /// Asynchronously counts every item that matches the <c>$filter</c> in <paramref name="options"/>,
    /// across all pages. <c>$orderby</c>, <c>$skip</c>, <c>$top</c>, and <c>$expand</c> are ignored,
    /// so the result is the total to pass to <see cref="PagedResponse.Create{T}"/> as <c>totalCount</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException">When <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidQueryOptionException">When the filter cannot be applied to <typeparamref name="T"/>.</exception>
    /// <exception cref="OperationCanceledException">When <paramref name="cancellationToken"/> is canceled.</exception>
    public static Task<int> CountAsync<T>(
        this IQueryable<T> query,
        ApiQueryOptions<T> options,
        CancellationToken cancellationToken = default) =>
        query.ApplyFilter(options).CountAsync(cancellationToken);

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

    /// <summary>
    /// Validates each segment of <paramref name="path"/> as a navigation, starting at
    /// <paramref name="entityType"/>, and returns the path with every segment in its declared casing.
    /// Navigations declared on derived types are found too, as EF Core's string <c>Include</c> does.
    /// </summary>
    /// <exception cref="InvalidQueryOptionException">When a segment is not a navigation.</exception>
    internal static string ResolveNavigationPath(IEntityType entityType, string path)
    {
        string[] segments = path.Split('.');
        IEntityType current = entityType;

        for (int i = 0; i < segments.Length; i++)
        {
            INavigationBase navigation = FindNavigation(current, segments[i])
                ?? throw new InvalidQueryOptionException(
                    "expand",
                    path,
                    FindProperty(current.ClrType, segments[i]) is { } property
                        ? $"Property '{property.Name}' on type '{current.ClrType.Name}' is not a navigation property."
                        : $"Property '{segments[i]}' not found on type '{current.ClrType.Name}'.");

            segments[i] = navigation.Name;
            current = navigation.TargetEntityType;
        }

        return string.Join('.', segments);
    }

    /// <summary>
    /// Returns the entity type that EF Core can <c>Include</c> on for <paramref name="query"/>, or
    /// <c>null</c> when it cannot. Starting at the last operator, it follows each operator's source
    /// down to the query root. EF Core can include only when that root is an entity
    /// query root (a <c>DbSet</c>, <c>FromSql</c>, and so on) of type <typeparamref name="T"/> and
    /// no operator on the way projects: any <c>Select</c> or <c>SelectMany</c>, or an operator that
    /// changes the element type. A projection to a DTO, or to a related entity through a navigation,
    /// returns <c>null</c>, as does a query that is not an EF Core query.
    /// </summary>
    internal static IEntityType? FindEntityType<T>(IQueryable<T> query)
    {
        Expression current = query.Expression;

        while (current is MethodCallExpression { Arguments.Count: > 0 } call && IsSameTypeOperator(call))
        {
            current = call.Arguments[0];
        }

        return current is EntityQueryRootExpression root && root.EntityType.ClrType == typeof(T)
            ? root.EntityType
            : null;
    }

    /// <summary>
    /// Returns <c>true</c> for an operator such as <c>Where</c>, <c>OrderBy</c>, <c>Take</c>, or
    /// <c>AsNoTracking</c> whose source and result are queries of the same element type.
    /// </summary>
    private static bool IsSameTypeOperator(MethodCallExpression call) =>
        call.Method.Name is not (nameof(Queryable.Select) or nameof(Queryable.SelectMany))
        && GetQueryElementType(call.Arguments[0].Type) is { } source
        && source == GetQueryElementType(call.Type);

    private static Type? GetQueryElementType(Type type) =>
        type.IsGenericType && typeof(IQueryable).IsAssignableFrom(type) ? type.GetGenericArguments()[0] : null;

    private static INavigationBase? FindNavigation(IEntityType entityType, string name)
    {
        INavigationBase? caseInsensitiveMatch = null;

        foreach (IEntityType type in entityType.GetDerivedTypesInclusive())
        {
            IEnumerable<INavigationBase> navigations = type.GetNavigations().Cast<INavigationBase>()
                .Concat(type.GetSkipNavigations());

            foreach (INavigationBase navigation in navigations)
            {
                if (string.Equals(navigation.Name, name, StringComparison.Ordinal))
                {
                    return navigation;
                }

                if (caseInsensitiveMatch is null && string.Equals(navigation.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    caseInsensitiveMatch = navigation;
                }
            }
        }

        return caseInsensitiveMatch;
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