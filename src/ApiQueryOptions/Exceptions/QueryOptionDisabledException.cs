namespace ApiQueryOptions.Exceptions;

/// <summary>
/// Thrown when an <see cref="ApiQueryOptionsSettings"/> flag disables a query option
/// but the caller explicitly tries to apply that option via an <c>Apply*</c> extension method.
/// </summary>
public sealed class QueryOptionDisabledException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance for the specified disabled option name.
    /// </summary>
    /// <param name="optionName">The name of the disabled query option, e.g. <c>"filter"</c> or <c>"orderby"</c>.</param>
    public QueryOptionDisabledException(string optionName)
        : base($"The '{optionName}' query option is disabled by ApiQueryOptionsSettings.")
    {
        OptionName = optionName;
    }

    /// <summary>
    /// The name of the disabled query option (e.g. "filter", "orderby").
    /// </summary>
    public string OptionName { get; }
}