namespace ApiQueryOptions.Exceptions;

/// <summary>
/// Thrown when a query option is well formed but cannot be applied to the queried type, for
/// example because it names a property that does not exist, uses a value that cannot be
/// converted to the property's type, or uses an operator the property's type does not support.
/// </summary>
public sealed class InvalidQueryOptionException : ApiQueryOptionsException
{
    /// <summary>
    /// Initializes a new instance for the specified option and property.
    /// </summary>
    /// <param name="optionName">The name of the query option, e.g. <c>"filter"</c>, <c>"orderby"</c>, or <c>"expand"</c>.</param>
    /// <param name="property">The property path the option refers to, or <c>null</c> when it does not refer to one.</param>
    /// <param name="message">The error message describing the failure.</param>
    public InvalidQueryOptionException(string optionName, string? property, string message)
        : this(optionName, property, message, inner: null)
    {
    }

    /// <summary>
    /// Initializes a new instance for the specified option and property with an inner exception.
    /// </summary>
    /// <param name="optionName">The name of the query option, e.g. <c>"filter"</c>, <c>"orderby"</c>, or <c>"expand"</c>.</param>
    /// <param name="property">The property path the option refers to, or <c>null</c> when it does not refer to one.</param>
    /// <param name="message">The error message describing the failure.</param>
    /// <param name="inner">The exception that caused this failure.</param>
    public InvalidQueryOptionException(string optionName, string? property, string message, Exception? inner)
        : base(message, inner)
    {
        OptionName = optionName;
        Property = property;
    }

    /// <summary>
    /// The name of the query option that could not be applied, e.g. <c>"filter"</c>, <c>"orderby"</c>, or <c>"expand"</c>.
    /// </summary>
    public string OptionName { get; }

    /// <summary>
    /// The property path the option refers to, or <c>null</c> when it does not refer to one.
    /// </summary>
    public string? Property { get; }
}
