namespace ApiQueryOptions.Filter;

/// <summary>
/// A logical combination of two sub-expressions: <c>left and|or right</c>.
/// </summary>
public sealed class LogicalFilterNode : FilterNode
{
    /// <summary>
    /// Initializes a new logical filter node.
    /// </summary>
    /// <param name="left">The left-hand sub-expression.</param>
    /// <param name="operator">The logical operator to apply.</param>
    /// <param name="right">The right-hand sub-expression.</param>
    public LogicalFilterNode(FilterNode left, LogicalOperator @operator, FilterNode right)
    {
        Left = left;
        Operator = @operator;
        Right = right;
    }

    /// <summary>
    /// The left-hand sub-expression.
    /// </summary>
    public FilterNode Left { get; }

    /// <summary>
    /// The logical operator combining the two sub-expressions.
    /// </summary>
    public LogicalOperator Operator { get; }

    /// <summary>
    /// The right-hand sub-expression.
    /// </summary>
    public FilterNode Right { get; }
}