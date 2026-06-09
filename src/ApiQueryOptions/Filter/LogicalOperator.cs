namespace ApiQueryOptions.Filter;

/// <summary>
/// Logical operators supported in <c>$filter</c> expressions.
/// </summary>
public enum LogicalOperator
{
    /// <summary>
    /// Both conditions must be true (<c>and</c>).
    /// </summary>
    And,

    /// <summary>
    /// At least one condition must be true (<c>or</c>).
    /// </summary>
    Or
}