namespace ApiQueryOptions.Filter;

/// <summary>
/// A string function call: <c>startsWith|endsWith|contains(property, 'value')</c>.
/// </summary>
public sealed class FunctionFilterNode : FilterNode
{
    /// <summary>
    /// Initializes a new function filter node.
    /// </summary>
    /// <param name="function">The string function to apply.</param>
    /// <param name="property">The property name to test.</param>
    /// <param name="value">The string value argument.</param>
    public FunctionFilterNode(StringFunction function, string property, string value)
    {
        Function = function;
        Property = property;
        Value = value;
    }

    /// <summary>
    /// The string function to apply.
    /// </summary>
    public StringFunction Function { get; }

    /// <summary>
    /// The property name to apply the function to.
    /// </summary>
    public string Property { get; }

    /// <summary>
    /// The string value argument passed to the function.
    /// </summary>
    public string Value { get; }
}