namespace ApiQueryOptions.Filter;

/// <summary>
/// A binary comparison node: <c>property op value</c>.
/// <para>Value types: <see cref="string"/>, <see cref="int"/>, <see cref="decimal"/>,
/// <see cref="bool"/>, <see cref="DateTimeOffset"/>, or <c>null</c>.</para>
/// </summary>
public sealed class BinaryFilterNode : FilterNode
{
    /// <summary>
    /// Initializes a new binary comparison node.
    /// </summary>
    /// <param name="property">The property path to compare.</param>
    /// <param name="operator">The comparison operator.</param>
    /// <param name="value">The right-hand value to compare against.</param>
    public BinaryFilterNode(string property, FilterOperator @operator, object? value)
    {
        Property = property;
        Operator = @operator;
        Value = value;
    }

    /// <summary>
    /// The comparison operator.
    /// </summary>
    public FilterOperator Operator { get; }

    /// <summary>
    /// The property path (dot-notation for nested properties).
    /// </summary>
    public string Property { get; }

    /// <summary>
    /// The typed right-hand value.
    /// </summary>
    public object? Value { get; }
}