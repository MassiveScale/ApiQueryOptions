namespace ApiQueryOptions.Exceptions;

/// <summary>
/// Base type for exceptions caused by invalid client input in a query option, such as a
/// <c>$filter</c> syntax error or an unknown property name. Catch this type to turn every
/// client mistake into a <c>400 Bad Request</c>.
/// </summary>
/// <remarks>
/// Configuration and programming errors, such as <see cref="QueryOptionDisabledException"/>,
/// do not derive from this type.
/// </remarks>
public abstract class ApiQueryOptionsException : Exception
{
    /// <summary>
    /// Initializes a new instance with the specified message.
    /// </summary>
    /// <param name="message">The error message describing the failure.</param>
    protected ApiQueryOptionsException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified message and inner exception.
    /// </summary>
    /// <param name="message">The error message describing the failure.</param>
    /// <param name="inner">The exception that caused this failure.</param>
    protected ApiQueryOptionsException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}
