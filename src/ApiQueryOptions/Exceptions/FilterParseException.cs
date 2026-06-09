namespace ApiQueryOptions.Exceptions;

/// <summary>
/// Thrown when a filter expression string cannot be parsed.
/// </summary>
public sealed class FilterParseException : Exception
{
    /// <summary>
    /// Initializes a new instance with the parse failure details.
    /// </summary>
    /// <param name="message">The error message describing the failure.</param>
    /// <param name="rawFilter">The raw filter string that caused the failure.</param>
    /// <param name="position">The approximate character position in the input where parsing failed. Defaults to <c>-1</c> (unknown).</param>
    public FilterParseException(string message, string rawFilter, int position = -1)
        : base(message)
    {
        RawFilter = rawFilter;
        Position = position;
    }

    /// <summary>
    /// Initializes a new instance with the parse failure details and an inner exception.
    /// </summary>
    /// <param name="message">The error message describing the failure.</param>
    /// <param name="rawFilter">The raw filter string that caused the failure.</param>
    /// <param name="position">The approximate character position in the input where parsing failed.</param>
    /// <param name="inner">The exception that caused this parse failure.</param>
    public FilterParseException(string message, string rawFilter, int position, Exception inner)
        : base(message, inner)
    {
        RawFilter = rawFilter;
        Position = position;
    }

    /// <summary>
    /// The approximate position in the input where parsing failed.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// The raw filter string that could not be parsed.
    /// </summary>
    public string RawFilter { get; }
}