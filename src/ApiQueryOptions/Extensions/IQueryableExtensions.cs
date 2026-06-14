using System.Linq.Expressions;
using System.Reflection;
using ApiQueryOptions.Exceptions;
using ApiQueryOptions.Filter;
using ApiQueryOptions.Options;

namespace ApiQueryOptions.Extensions;

/// <summary>
/// IQueryable&lt;T&gt; extension methods that compose deferred LINQ expression trees.
/// All methods operate on the query object only — nothing is enumerated.
/// </summary>
public static class IQueryableExtensions
{
    /// <summary>
    /// Applies all non-null options from <paramref name="options"/> to <paramref name="query"/>
    /// in the order: Filter → OrderBy → Skip → Top.
    /// <c>Expand</c> is not applied here; use the EF Core companion package.
    /// </summary>
    public static IQueryable<T> Apply<T>(this IQueryable<T> query, ApiQueryOptions<T> options)
    {
        if (options.Filter is not null)
        {
            query = query.ApplyFilter(options.Filter, options.Settings);
        }

        if (options.OrderBy is not null)
        {
            query = query.ApplyOrderBy(options.OrderBy, options.Settings);
        }

        if (options.Skip is not null)
        {
            query = query.ApplySkip(options.Skip, options.Settings);
        }

        if (options.Top is not null)
        {
            query = query.ApplyTop(options.Top, options.Settings);
        }

        return query;
    }

    /// <summary>
    /// Applies a <c>$filter</c> expression as a deferred <c>.Where()</c> call.
    /// </summary>
    /// <exception cref="QueryOptionDisabledException">When <c>settings.FilterEnabled</c> is <c>false</c>.</exception>
    public static IQueryable<T> ApplyFilter<T>(
        this IQueryable<T> query,
        FilterQueryOption filter,
        ApiQueryOptionsSettings settings)
    {
        if (!settings.FilterEnabled)
        {
            throw new QueryOptionDisabledException("filter");
        }

        ParameterExpression param = Expression.Parameter(typeof(T), "x");
        Expression body = BuildFilterExpression(filter.FilterClause.Expression, param, settings);
        var lambda = Expression.Lambda<Func<T, bool>>(body, param);
        return query.Where(lambda);
    }

    /// <summary>
    /// Applies a <c>$orderby</c> expression as a deferred <c>.OrderBy()</c> chain.
    /// </summary>
    /// <exception cref="QueryOptionDisabledException">When <c>settings.OrderByEnabled</c> is <c>false</c>.</exception>
    public static IQueryable<T> ApplyOrderBy<T>(
        this IQueryable<T> query,
        OrderByQueryOption orderBy,
        ApiQueryOptionsSettings settings)
    {
        if (!settings.OrderByEnabled)
        {
            throw new QueryOptionDisabledException("orderby");
        }

        IOrderedQueryable<T>? ordered = null;
        foreach (OrderByItem item in orderBy.Items)
        {
            ParameterExpression param = Expression.Parameter(typeof(T), "x");
            MemberExpression prop = BuildPropertyAccess(param, item.Property);
            LambdaExpression keySelector = Expression.Lambda(prop, param);

            string method = ordered is null
                ? (item.Descending ? "OrderByDescending" : "OrderBy")
                : (item.Descending ? "ThenByDescending" : "ThenBy");

            Type resultType = prop.Type;
            MethodCallExpression call = Expression.Call(
                typeof(Queryable),
                method,
                [typeof(T), resultType],
                (ordered is null ? query : ordered).Expression,
                Expression.Quote(keySelector));

            ordered = (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(call);
        }

        return ordered ?? query;
    }

    /// <summary>
    /// Applies a <c>$skip</c> value as a deferred <c>.Skip()</c> call.
    /// </summary>
    /// <exception cref="QueryOptionDisabledException">When <c>settings.SkipEnabled</c> is <c>false</c>.</exception>
    public static IQueryable<T> ApplySkip<T>(
        this IQueryable<T> query,
        SkipQueryOption skip,
        ApiQueryOptionsSettings settings)
    {
        if (!settings.SkipEnabled)
        {
            throw new QueryOptionDisabledException("skip");
        }

        return query.Skip(skip.Value);
    }

    /// <summary>
    /// Applies a <c>$top</c> value as a deferred <c>.Take()</c> call.
    /// </summary>
    /// <exception cref="QueryOptionDisabledException">When <c>settings.TopEnabled</c> is <c>false</c>.</exception>
    public static IQueryable<T> ApplyTop<T>(
        this IQueryable<T> query,
        TopQueryOption top,
        ApiQueryOptionsSettings settings)
    {
        if (!settings.TopEnabled)
        {
            throw new QueryOptionDisabledException("top");
        }

        return query.Take(top.Value);
    }

    private static Expression BuildBinaryExpression(
        BinaryFilterNode node,
        ParameterExpression param,
        ApiQueryOptionsSettings settings)
    {
        MemberExpression propExpr = BuildPropertyAccess(param, node.Property);

        // Convert the constant to the property's type (handles nullable types too)
        Type targetType = Nullable.GetUnderlyingType(propExpr.Type) ?? propExpr.Type;
        object? converted = node.Value is null ? null : Convert.ChangeType(node.Value, targetType);
        ConstantExpression constExpr = Expression.Constant(converted, propExpr.Type);

        // For string eq/ne with OrdinalIgnoreCase, use .ToUpper() for EF Core compatibility
        if (propExpr.Type == typeof(string) && node.Operator is FilterOperator.Eq or FilterOperator.Ne &&
            settings.StringComparison == StringComparison.OrdinalIgnoreCase)
        {
            MethodInfo toUpperMethod = typeof(string).GetMethod(nameof(string.ToUpper), [])!;
            Expression propUpper = Expression.Call(propExpr, toUpperMethod);
            Expression constUpper = Expression.Call(constExpr, toUpperMethod);

            BinaryExpression equalExpr = Expression.Equal(propUpper, constUpper);
            return node.Operator == FilterOperator.Ne
                ? Expression.Not(equalExpr)
                : (Expression)equalExpr;
        }

        return node.Operator switch
        {
            FilterOperator.Eq => Expression.Equal(propExpr, constExpr),
            FilterOperator.Ne => Expression.NotEqual(propExpr, constExpr),
            FilterOperator.Lt => Expression.LessThan(propExpr, constExpr),
            FilterOperator.Gt => Expression.GreaterThan(propExpr, constExpr),
            FilterOperator.Le => Expression.LessThanOrEqual(propExpr, constExpr),
            FilterOperator.Ge => Expression.GreaterThanOrEqual(propExpr, constExpr),
            _ => throw new NotSupportedException($"Unsupported operator: {node.Operator}") // coverage: exclude
        };
    }

    private static Expression BuildFilterExpression(
            FilterNode node,
        ParameterExpression param,
        ApiQueryOptionsSettings settings)
    {
        return node switch
        {
            BinaryFilterNode bin => BuildBinaryExpression(bin, param, settings),
            LogicalFilterNode logical => BuildLogicalExpression(logical, param, settings),
            FunctionFilterNode func => BuildFunctionExpression(func, param, settings),
            _ => throw new NotSupportedException($"Unsupported filter node type: {node.GetType().Name}") // coverage: exclude
        };
    }

    private static Expression BuildFunctionExpression(
        FunctionFilterNode node,
        ParameterExpression param,
        ApiQueryOptionsSettings settings)
    {
        MemberExpression propExpr = BuildPropertyAccess(param, node.Property);
        ConstantExpression valueExpr = Expression.Constant(node.Value);

        string methodName = node.Function switch
        {
            StringFunction.StartsWith => nameof(string.StartsWith),
            StringFunction.EndsWith => nameof(string.EndsWith),
            StringFunction.Contains => nameof(string.Contains),
            _ => throw new NotSupportedException($"Unsupported string function: {node.Function}") // coverage: exclude
        };

        // For OrdinalIgnoreCase, use .ToUpper() on both sides for EF Core compatibility
        if (settings.StringComparison == StringComparison.OrdinalIgnoreCase)
        {
            MethodInfo toUpperMethod = typeof(string).GetMethod(nameof(string.ToUpper), [])!;
            Expression propUpper = Expression.Call(propExpr, toUpperMethod);
            Expression valueUpper = Expression.Call(valueExpr, toUpperMethod);
            MethodInfo method = typeof(string).GetMethod(methodName, [typeof(string)])!;
            return Expression.Call(propUpper, method, valueUpper);
        }

        MethodInfo caseSensitiveMethod = typeof(string).GetMethod(methodName, [typeof(string)])!;
        return Expression.Call(propExpr, caseSensitiveMethod, valueExpr);
    }

    private static Expression BuildLogicalExpression(
            LogicalFilterNode node,
        ParameterExpression param,
        ApiQueryOptionsSettings settings)
    {
        Expression left = BuildFilterExpression(node.Left, param, settings);
        Expression right = BuildFilterExpression(node.Right, param, settings);

        return node.Operator switch
        {
            LogicalOperator.And => Expression.AndAlso(left, right),
            LogicalOperator.Or => Expression.OrElse(left, right),
            _ => throw new NotSupportedException($"Unsupported logical operator: {node.Operator}") // coverage: exclude
        };
    }

    /// <summary>
    /// Builds a property access expression supporting dot-notation for nested properties.
    /// </summary>
    private static MemberExpression BuildPropertyAccess(Expression root, string propertyPath)
    {
        string[] parts = propertyPath.Split('.');
        Expression current = root;
        MemberExpression? memberExpr = null;

        foreach (string part in parts)
        {
            PropertyInfo prop = current.Type.GetProperty(part,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException(
                    $"Property '{part}' not found on type '{current.Type.Name}'.");

            memberExpr = Expression.Property(current, prop);
            current = memberExpr;
        }

        return memberExpr!;
    }
}