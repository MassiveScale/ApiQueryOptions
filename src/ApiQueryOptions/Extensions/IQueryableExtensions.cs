using System.Collections.Concurrent;
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
    private const string FilterOptionName = "filter";
    private const string OrderByOptionName = "orderby";

    private static readonly MethodInfo _compareMethod =
        typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;
    private static readonly MethodInfo _compareOrdinalMethod =
        typeof(string).GetMethod(nameof(string.CompareOrdinal), [typeof(string), typeof(string)])!;
    private static readonly MethodInfo _compareWithComparisonMethod =
        typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string), typeof(StringComparison)])!;
    private static readonly MethodInfo _containsMethod =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly MethodInfo _containsWithComparisonMethod =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string), typeof(StringComparison)])!;
    private static readonly MethodInfo _createValueAccessMethod =
        typeof(IQueryableExtensions).GetMethod(nameof(CreateValueAccess), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo _endsWithMethod =
        typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;
    private static readonly MethodInfo _endsWithWithComparisonMethod =
        typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string), typeof(StringComparison)])!;
    private static readonly MethodInfo _equalsWithComparisonMethod =
        typeof(string).GetMethod(nameof(string.Equals), [typeof(string), typeof(string), typeof(StringComparison)])!;
    private static readonly MethodInfo _startsWithMethod =
        typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
    private static readonly MethodInfo _startsWithWithComparisonMethod =
        typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string), typeof(StringComparison)])!;
    private static readonly MethodInfo _toUpperMethod =
        typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!;
    private static readonly ConcurrentDictionary<Type, Func<object, Expression>> _valueAccessFactories = new();

    /// <summary>
    /// How string comparisons are built for one query.
    /// </summary>
    private enum StringMode
    {
        /// <summary>
        /// <see cref="StringComparison.OrdinalIgnoreCase"/> overloads, for in-memory sources.
        /// </summary>
        OrdinalIgnoreCase,

        /// <summary>
        /// <see cref="StringComparison.Ordinal"/> overloads, for in-memory sources.
        /// </summary>
        Ordinal,

        /// <summary>
        /// <c>UPPER(column)</c> compared with a value upper-cased on the client, for database providers.
        /// </summary>
        Upper,

        /// <summary>
        /// Plain operators and methods with nothing added.
        /// </summary>
        Plain,
    }

    /// <summary>
    /// Applies all non-null options from <paramref name="options"/> to <paramref name="query"/>
    /// in the order: Filter → OrderBy → Skip → Top → Expand.
    /// When <see cref="ApiQueryOptionsSettings.TieBreakerProperty"/> is set and <c>$top</c> or
    /// <c>$skip</c> is applied, the tie-breaker is appended as the last sort column unless
    /// <c>$orderby</c> already includes it.
    /// <c>Expand</c> is applied as EF Core <c>.Include()</c> calls when the
    /// <c>ApiQueryOptions.EntityFrameworkCore</c> package is installed; no <c>using</c> for that
    /// package is needed. Without it, or for value-type entities, <c>Expand</c> is ignored.
    /// </summary>
    /// <exception cref="InvalidQueryOptionException">
    /// When an option names an unknown property or cannot be applied to <typeparamref name="T"/>,
    /// or when <see cref="ApiQueryOptionsSettings.TieBreakerProperty"/> names an unknown property.
    /// </exception>
    public static IQueryable<T> Apply<T>(this IQueryable<T> query, ApiQueryOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Filter is not null)
        {
            query = query.ApplyFilter(options.Filter, options.Settings);
        }

        if (options.OrderBy is not null)
        {
            query = query.ApplyOrderBy(options.OrderBy, options.Settings);
        }

        if (options.Skip is not null || options.Top is not null)
        {
            query = ApplyTieBreaker(query, options.OrderBy, options.Settings.TieBreakerProperty);
        }

        if (options.Skip is not null)
        {
            query = query.ApplySkip(options.Skip, options.Settings);
        }

        if (options.Top is not null)
        {
            query = query.ApplyTop(options.Top, options.Settings);
        }

        if (options.Expand is not null)
        {
            query = ExpandApplier.Apply(query, options.Expand, options.Settings);
        }

        return query;
    }

    /// <summary>
    /// Applies a <c>$filter</c> expression as a deferred <c>.Where()</c> call. String comparisons
    /// follow <see cref="ApiQueryOptionsSettings.StringMatching"/>.
    /// </summary>
    /// <exception cref="QueryOptionDisabledException">When <c>settings.FilterEnabled</c> is <c>false</c>.</exception>
    /// <exception cref="FilterParseException">When the filter expression cannot be parsed.</exception>
    /// <exception cref="InvalidQueryOptionException">
    /// When the filter names an unknown property, uses a value that cannot be converted to the
    /// property's type, uses a string function on a non-string property, compares a non-nullable
    /// value type with <c>null</c>, or uses an operator the property's type does not support.
    /// </exception>
    public static IQueryable<T> ApplyFilter<T>(
        this IQueryable<T> query,
        FilterQueryOption filter,
        ApiQueryOptionsSettings settings)
    {
        if (!settings.FilterEnabled)
        {
            throw new QueryOptionDisabledException("filter");
        }

        StringMode mode = GetStringMode(settings.StringMatching, inMemory: query.Provider is EnumerableQuery);
        ParameterExpression param = Expression.Parameter(typeof(T), "x");
        Expression body = BuildFilterExpression(filter.FilterClause.Expression, param, mode);
        var lambda = Expression.Lambda<Func<T, bool>>(body, param);
        return query.Where(lambda);
    }

    /// <summary>
    /// Applies a <c>$orderby</c> expression as a deferred <c>.OrderBy()</c> chain.
    /// </summary>
    /// <exception cref="QueryOptionDisabledException">When <c>settings.OrderByEnabled</c> is <c>false</c>.</exception>
    /// <exception cref="InvalidQueryOptionException">When the orderby names an unknown property.</exception>
    public static IQueryable<T> ApplyOrderBy<T>(
        this IQueryable<T> query,
        OrderByQueryOption orderBy,
        ApiQueryOptionsSettings settings)
    {
        if (!settings.OrderByEnabled)
        {
            throw new QueryOptionDisabledException("orderby");
        }

        IQueryable<T> ordered = query;
        bool isOrdered = false;
        foreach (OrderByItem item in orderBy.Items)
        {
            ordered = OrderByProperty(ordered, item.Property, item.Descending, thenBy: isOrdered);
            isOrdered = true;
        }

        return ordered;
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

    private static IQueryable<T> ApplyTieBreaker<T>(IQueryable<T> query, OrderByQueryOption? orderBy, string? tieBreaker)
    {
        if (tieBreaker is null)
        {
            return query;
        }

        bool isOrdered = orderBy is { Items.Count: > 0 };
        if (isOrdered && orderBy!.Items.Any(item => string.Equals(item.Property, tieBreaker, StringComparison.OrdinalIgnoreCase)))
        {
            return query;
        }

        return OrderByProperty(query, tieBreaker, descending: false, thenBy: isOrdered);
    }

    private static Expression BuildBinaryExpression(BinaryFilterNode node, ParameterExpression param, StringMode mode)
    {
        MemberExpression propExpr = BuildPropertyAccess(param, node.Property, FilterOptionName);

        if (node.Value is null)
        {
            return BuildNullComparison(node, propExpr);
        }

        Type targetType = Nullable.GetUnderlyingType(propExpr.Type) ?? propExpr.Type;
        object converted = FilterValueConverter.Convert(node.Value, targetType, node.Property);

        if (propExpr.Type == typeof(string))
        {
            return BuildStringComparison(node.Operator, propExpr, (string)converted, mode);
        }

        Expression constExpr = BuildValueExpression(converted, propExpr.Type);
        return BuildOperator(node, () => node.Operator switch
        {
            FilterOperator.Eq => Expression.Equal(propExpr, constExpr),
            FilterOperator.Ne => Expression.NotEqual(propExpr, constExpr),
            FilterOperator.Lt => Expression.LessThan(propExpr, constExpr),
            FilterOperator.Gt => Expression.GreaterThan(propExpr, constExpr),
            FilterOperator.Le => Expression.LessThanOrEqual(propExpr, constExpr),
            FilterOperator.Ge => Expression.GreaterThanOrEqual(propExpr, constExpr),
            _ => throw new NotSupportedException($"Unsupported operator: {node.Operator}") // coverage: exclude
        });
    }

    private static Expression BuildFilterExpression(FilterNode node, ParameterExpression param, StringMode mode)
    {
        return node switch
        {
            BinaryFilterNode bin => BuildBinaryExpression(bin, param, mode),
            LogicalFilterNode logical => BuildLogicalExpression(logical, param, mode),
            FunctionFilterNode func => BuildFunctionExpression(func, param, mode),
            _ => throw new NotSupportedException($"Unsupported filter node type: {node.GetType().Name}") // coverage: exclude
        };
    }

    private static Expression BuildFunctionExpression(FunctionFilterNode node, ParameterExpression param, StringMode mode)
    {
        MemberExpression propExpr = BuildPropertyAccess(param, node.Property, FilterOptionName);

        if (propExpr.Type != typeof(string))
        {
            throw new InvalidQueryOptionException(
                FilterOptionName,
                node.Property,
                $"The '{node.Function}' function requires a string property, but '{node.Property}' is of type '{propExpr.Type.Name}'.");
        }

        (MethodInfo plain, MethodInfo withComparison) = node.Function switch
        {
            StringFunction.StartsWith => (_startsWithMethod, _startsWithWithComparisonMethod),
            StringFunction.EndsWith => (_endsWithMethod, _endsWithWithComparisonMethod),
            StringFunction.Contains => (_containsMethod, _containsWithComparisonMethod),
            _ => throw new NotSupportedException($"Unsupported string function: {node.Function}") // coverage: exclude
        };

        Expression call = mode switch
        {
            StringMode.OrdinalIgnoreCase => Expression.Call(
                propExpr, withComparison, BuildValueExpression(node.Value, typeof(string)), Expression.Constant(StringComparison.OrdinalIgnoreCase)),
            StringMode.Ordinal => Expression.Call(
                propExpr, withComparison, BuildValueExpression(node.Value, typeof(string)), Expression.Constant(StringComparison.Ordinal)),
            StringMode.Upper => Expression.Call(
                Expression.Call(propExpr, _toUpperMethod), plain, BuildValueExpression(node.Value.ToUpperInvariant(), typeof(string))),
            _ => Expression.Call(propExpr, plain, BuildValueExpression(node.Value, typeof(string))),
        };

        // A null property never matches a string function, in memory or in the database.
        return NotNullAnd(propExpr, call);
    }

    private static Expression BuildLogicalExpression(LogicalFilterNode node, ParameterExpression param, StringMode mode)
    {
        Expression left = BuildFilterExpression(node.Left, param, mode);
        Expression right = BuildFilterExpression(node.Right, param, mode);

        return node.Operator switch
        {
            LogicalOperator.And => Expression.AndAlso(left, right),
            LogicalOperator.Or => Expression.OrElse(left, right),
            _ => throw new NotSupportedException($"Unsupported logical operator: {node.Operator}") // coverage: exclude
        };
    }

    private static Expression BuildNullComparison(BinaryFilterNode node, MemberExpression propExpr)
    {
        if (propExpr.Type.IsValueType && Nullable.GetUnderlyingType(propExpr.Type) is null)
        {
            throw new InvalidQueryOptionException(
                FilterOptionName,
                node.Property,
                $"Property '{node.Property}' of type '{propExpr.Type.Name}' cannot be compared with null.");
        }

        Expression nullExpr = Expression.Constant(null, propExpr.Type);
        return node.Operator switch
        {
            FilterOperator.Eq => BuildOperator(node, () => Expression.Equal(propExpr, nullExpr)),
            FilterOperator.Ne => BuildOperator(node, () => Expression.NotEqual(propExpr, nullExpr)),
            _ => throw new InvalidQueryOptionException(
                FilterOptionName,
                node.Property,
                $"The '{node.Operator}' operator cannot be used with null for property '{node.Property}'."),
        };
    }

    /// <summary>
    /// Builds a comparison operator, turning the <see cref="InvalidOperationException"/> that
    /// <see cref="Expression"/> throws for an operator the type does not define into an
    /// <see cref="InvalidQueryOptionException"/>.
    /// </summary>
    private static Expression BuildOperator(BinaryFilterNode node, Func<Expression> build)
    {
        try
        {
            return build();
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidQueryOptionException(
                FilterOptionName,
                node.Property,
                $"The '{node.Operator}' operator is not supported for property '{node.Property}'.",
                ex);
        }
    }

    /// <summary>
    /// Builds a property access expression supporting dot-notation for nested properties.
    /// </summary>
    private static MemberExpression BuildPropertyAccess(Expression root, string propertyPath, string optionName)
    {
        Expression current = root;

        foreach (string part in propertyPath.Split('.'))
        {
            PropertyInfo prop = PropertyCache.Find(current.Type, part)
                ?? throw new InvalidQueryOptionException(
                    optionName,
                    propertyPath,
                    $"Property '{part}' not found on type '{current.Type.Name}'.");

            current = Expression.Property(current, prop);
        }

        return (MemberExpression)current;
    }

    private static Expression BuildStringComparison(FilterOperator op, MemberExpression propExpr, string value, StringMode mode)
    {
        Expression valueExpr = BuildValueExpression(mode == StringMode.Upper ? value.ToUpperInvariant() : value, typeof(string));
        Expression left = mode == StringMode.Upper ? Expression.Call(propExpr, _toUpperMethod) : propExpr;

        if (op is FilterOperator.Eq or FilterOperator.Ne)
        {
            Expression equal = mode switch
            {
                StringMode.OrdinalIgnoreCase => Expression.Call(
                    _equalsWithComparisonMethod, propExpr, valueExpr, Expression.Constant(StringComparison.OrdinalIgnoreCase)),
                StringMode.Upper => NotNullAnd(propExpr, Expression.Equal(left, valueExpr)),
                _ => Expression.Equal(propExpr, valueExpr),
            };

            return op == FilterOperator.Eq ? equal : Expression.Not(equal);
        }

        // EF Core translates 'string.Compare(a, b) op 0' into 'a op b'.
        Expression compare = mode switch
        {
            StringMode.OrdinalIgnoreCase => Expression.Call(
                _compareWithComparisonMethod, propExpr, valueExpr, Expression.Constant(StringComparison.OrdinalIgnoreCase)),
            StringMode.Ordinal => Expression.Call(_compareOrdinalMethod, propExpr, valueExpr),
            _ => Expression.Call(_compareMethod, left, valueExpr),
        };

        Expression zero = Expression.Constant(0);
        Expression result = op switch
        {
            FilterOperator.Lt => Expression.LessThan(compare, zero),
            FilterOperator.Gt => Expression.GreaterThan(compare, zero),
            FilterOperator.Le => Expression.LessThanOrEqual(compare, zero),
            FilterOperator.Ge => Expression.GreaterThanOrEqual(compare, zero),
            _ => throw new NotSupportedException($"Unsupported operator: {op}") // coverage: exclude
        };

        return mode == StringMode.Upper ? NotNullAnd(propExpr, result) : result;
    }

    /// <summary>
    /// Builds the expression for a filter value. Non-null values are read through a property of a
    /// captured holder object rather than embedded as a constant, so EF Core sends them as SQL
    /// parameters. Inlined literals would give every distinct value its own EF query-cache entry
    /// and its own database execution plan.
    /// </summary>
    private static Expression BuildValueExpression(object value, Type type)
    {
        Func<object, Expression> factory = _valueAccessFactories.GetOrAdd(type, static t =>
            _createValueAccessMethod.MakeGenericMethod(t).CreateDelegate<Func<object, Expression>>());
        return factory(value);
    }

    private static Expression CreateValueAccess<TValue>(object value) =>
        Expression.Property(
            Expression.Constant(new FilterValue<TValue>((TValue)value)),
            nameof(FilterValue<TValue>.Value));

    private static StringMode GetStringMode(StringMatching matching, bool inMemory)
    {
        return matching switch
        {
            StringMatching.IgnoreCase => inMemory ? StringMode.OrdinalIgnoreCase : StringMode.Upper,
            StringMatching.CaseSensitive => inMemory ? StringMode.Ordinal : StringMode.Plain,
            _ => StringMode.Plain,
        };
    }

    /// <summary>
    /// Returns <c>property != null &amp;&amp; <paramref name="expression"/></c>, so in-memory
    /// sources never call a method on a null string.
    /// </summary>
    private static Expression NotNullAnd(MemberExpression property, Expression expression) =>
        Expression.AndAlso(Expression.NotEqual(property, Expression.Constant(null, typeof(string))), expression);

    private static IQueryable<T> OrderByProperty<T>(IQueryable<T> query, string propertyPath, bool descending, bool thenBy)
    {
        ParameterExpression param = Expression.Parameter(typeof(T), "x");
        MemberExpression prop = BuildPropertyAccess(param, propertyPath, OrderByOptionName);
        LambdaExpression keySelector = Expression.Lambda(prop, param);

        string method = thenBy
            ? (descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy))
            : (descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy));

        MethodCallExpression call = Expression.Call(
            typeof(Queryable),
            method,
            [typeof(T), prop.Type],
            query.Expression,
            Expression.Quote(keySelector));

        return query.Provider.CreateQuery<T>(call);
    }

    /// <summary>
    /// Holds a filter value so expression trees can read it as a member access instead of a constant.
    /// </summary>
    private sealed class FilterValue<TValue>(TValue value)
    {
        /// <summary>
        /// The filter value.
        /// </summary>
        public TValue Value { get; } = value;
    }
}
