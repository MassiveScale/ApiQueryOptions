namespace ApiQueryOptions.Filter;

/// <summary>
/// Root wrapper for a parsed filter expression AST.
/// </summary>
public sealed class FilterClause
{
    /// <summary>
    /// Initializes a new instance wrapping the given root <paramref name="expression"/> node.
    /// </summary>
    /// <param name="expression">The root node of the parsed filter AST.</param>
    public FilterClause(FilterNode expression)
    {
        Expression = expression;
    }

    /// <summary>
    /// The root node of the expression tree.
    /// </summary>
    public FilterNode Expression { get; }
}