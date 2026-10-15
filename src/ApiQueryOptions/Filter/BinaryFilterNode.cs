namespace ApiQueryOptions.Filter;

/// <summary>
/// A binary comparison node: <c>property op value</c>.
/// <para>Value types produced by <see cref="FilterParser"/>: <see cref="string"/>,
/// <see cref="int"/>, <see cref="long"/> (integers outside the <see cref="int"/> range),
/// <see cref="decimal"/> (numbers with a fraction, or integers outside the <see cref="long"/> range),
/// <see cref="bool"/>, or <c>null</c>. Dates, times, GUIDs, and enum names are written as strings
/// and converted to the property's type when the filter is applied.</para>
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