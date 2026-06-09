using ApiQueryOptions.Filter;

namespace ApiQueryOptions.Options;

/// <summary>
/// Parsed representation of the <c>$filter</c> query parameter.
/// The <see cref="FilterClause"/> is populated lazily on first access (thread-safe).
/// </summary>
public sealed class FilterQueryOption
{
    private FilterClause? _filterClause;
    private int _parsed;
    private Exception? _parseError;
    // 0 = not parsed, 1 = parsed (Interlocked)

    /// <summary>
    /// Initializes a new instance with the given unparsed filter expression.
    /// Parsing is deferred until <see cref="FilterClause"/> is first accessed.
    /// </summary>
    /// <param name="rawValue">The raw <c>$filter</c> query string value.</param>
    public FilterQueryOption(string rawValue)
    {
        RawValue = rawValue;
    }

    /// <summary>
    /// The parsed AST. Populated lazily on first access.
    /// Throws <see cref="Exceptions.FilterParseException"/> if the raw value cannot be parsed.
    /// </summary>
    public FilterClause FilterClause
    {
        get
        {
            if (System.Threading.Interlocked.Exchange(ref _parsed, 1) == 0)
            {
                try
                {
                    _filterClause = new FilterParser().Parse(RawValue);
                }
                catch (Exception ex)
                {
                    _parseError = ex;
                }
            }

            if (_parseError is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_parseError).Throw();
            }

            return _filterClause!;
        }
    }

    /// <summary>
    /// The raw filter string as supplied in the query string.
    /// </summary>
    public string RawValue { get; }
}