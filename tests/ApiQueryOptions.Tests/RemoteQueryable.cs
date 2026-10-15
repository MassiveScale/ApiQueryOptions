using System.Collections;
using System.Linq.Expressions;

namespace ApiQueryOptions.Tests;

/// <summary>
/// A queryable whose provider is not <see cref="EnumerableQuery"/>, so the library builds the
/// expressions it would send to a database provider. It records expressions and cannot run them;
/// <see cref="Run{T}"/> compiles the <c>Where</c> predicate to check its behavior in .NET.
/// </summary>
internal sealed class RemoteQueryable<T> : IQueryable<T>, IQueryProvider
{
    public RemoteQueryable()
    {
        Expression = Expression.Constant(this);
    }

    private RemoteQueryable(Expression expression)
    {
        Expression = expression;
    }

    public Type ElementType => typeof(T);

    public Expression Expression { get; }

    public IQueryProvider Provider => this;

    public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new RemoteQueryable<TElement>(expression);

    public object? Execute(Expression expression) => throw new NotSupportedException();

    public TResult Execute<TResult>(Expression expression) => throw new NotSupportedException();

    public IEnumerator<T> GetEnumerator() => throw new NotSupportedException();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Helpers for inspecting expression trees built by the library.
/// </summary>
internal static class ExpressionInspector
{
    /// <summary>
    /// Returns the value of every <see cref="ConstantExpression"/> in <paramref name="expression"/>.
    /// </summary>
    public static List<object?> Constants(Expression expression)
    {
        var collector = new Collector();
        collector.Visit(expression);
        return collector.Constants;
    }

    /// <summary>
    /// Returns the names of every method called anywhere in <paramref name="expression"/>.
    /// </summary>
    public static List<string> MethodNames(Expression expression)
    {
        var collector = new Collector();
        collector.Visit(expression);
        return collector.Methods;
    }

    /// <summary>
    /// Returns the predicate passed to the outermost <c>Where</c> call in <paramref name="query"/>.
    /// </summary>
    public static Expression<Func<T, bool>> Predicate<T>(IQueryable<T> query)
    {
        var where = (MethodCallExpression)query.Expression;
        return (Expression<Func<T, bool>>)((UnaryExpression)where.Arguments[1]).Operand;
    }

    /// <summary>
    /// Compiles the <c>Where</c> predicate of <paramref name="query"/> and applies it to <paramref name="data"/>.
    /// </summary>
    public static List<T> Run<T>(IQueryable<T> query, IEnumerable<T> data) =>
        data.Where(Predicate(query).Compile()).ToList();

    /// <summary>
    /// Returns the values read from filter value holders (member accesses on constants).
    /// </summary>
    public static List<object?> ParameterValues(Expression expression)
    {
        var collector = new Collector();
        collector.Visit(expression);
        return collector.Parameters;
    }

    private sealed class Collector : ExpressionVisitor
    {
        public List<object?> Constants { get; } = [];

        public List<string> Methods { get; } = [];

        public List<object?> Parameters { get; } = [];

        protected override Expression VisitConstant(ConstantExpression node)
        {
            Constants.Add(node.Value);
            return base.VisitConstant(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ConstantExpression { Value: { } holder } && node.Member is System.Reflection.PropertyInfo property)
            {
                Parameters.Add(property.GetValue(holder));
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Methods.Add(node.Method.Name);
            return base.VisitMethodCall(node);
        }
    }
}
