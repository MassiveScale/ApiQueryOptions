using ApiQueryOptions.Filter;

namespace ApiQueryOptions.Options;

/// <summary>
/// Parsed representation of the <c>$filter</c> query parameter.
/// The <see cref="FilterClause"/> is populated lazily on first access (thread-safe).
/// </summary>
public sealed class FilterQueryOption
{
    private readonly Lazy<FilterClause> filterClause;

    /// <summary>
    /// Initializes a new instance with the given unparsed filter expression, using the default
    /// nesting depth and node count limits.
    /// Parsing is deferred until <see cref="FilterClause"/> is first accessed.
    /// </summary>
    /// <param name="rawValue">The raw <c>$filter</c> query string value.</param>
    public FilterQueryOption(string rawValue)
        : this(rawValue, ApiQueryOptionsSettings.DefaultMaxFilterDepth, ApiQueryOptionsSettings.DefaultMaxFilterNodeCount)
    {
    }

    /// <summary>
    /// Initializes a new instance with the given unparsed filter expression, using the
    /// nesting depth and node count limits from <paramref name="settings"/>.
    /// Parsing is deferred until <see cref="FilterClause"/> is first accessed.
    /// </summary>
    /// <param name="rawValue">The raw <c>$filter</c> query string value.</param>
    /// <param name="settings">
    /// The settings that supply <see cref="ApiQueryOptionsSettings.MaxFilterDepth"/> and
    /// <see cref="ApiQueryOptionsSettings.MaxFilterNodeCount"/>.
    /// </param>
    public FilterQueryOption(string rawValue, ApiQueryOptionsSettings settings)
        : this(rawValue, settings?.MaxFilterDepth ?? throw new ArgumentNullException(nameof(settings)), settings.MaxFilterNodeCount)
    {
    }

    private FilterQueryOption(string rawValue, int maxDepth, int maxNodeCount)
    {
        RawValue = rawValue;

        // ExecutionAndPublication guarantees a single parse even under concurrent first access,
        // and caches a parse failure so every reader observes the same exception.
        filterClause = new Lazy<FilterClause>(
            () => new FilterParser(maxDepth, maxNodeCount).Parse(rawValue),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// The parsed AST. Populated lazily on first access.
    /// Throws <see cref="Exceptions.FilterParseException"/> if the raw value cannot be parsed.
    /// </summary>
    public FilterClause FilterClause => filterClause.Value;

    /// <summary>
    /// The raw filter string as supplied in the query string.
    /// </summary>
    public string RawValue { get; }
}
