namespace ApiQueryOptions;

/// <summary>
/// Controls which query options are accepted and parsed, and sets global pagination defaults.
/// All options are enabled by default. Pass an instance to <see cref="ApiQueryOptions{T}"/> or
/// register via <c>AddApiQueryOptions()</c>.
/// </summary>
public sealed class ApiQueryOptionsSettings
{
    /// <summary>
    /// The default number of items to return when the caller omits <c>$top</c>.
    /// <c>null</c> means no default is applied and unbounded results are returned when
    /// <c>$top</c> is absent. Default: <c>null</c>.
    /// </summary>
    public int? DefaultPageSize { get; init; }

    /// <summary>
    /// Whether the <c>$expand</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool ExpandEnabled { get; init; } = true;

    /// <summary>
    /// The query parameter names recognised as the <c>$expand</c> option, tried left-to-right.
    /// Default: <c>["$expand", "expand"]</c>.
    /// </summary>
    public IReadOnlyList<string> ExpandParameterNames { get; init; } = ["$expand", "expand"];

    /// <summary>
    /// Whether the <c>$filter</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool FilterEnabled { get; init; } = true;

    /// <summary>
    /// The query parameter names recognised as the <c>$filter</c> option, tried left-to-right.
    /// Default: <c>["$filter", "filter"]</c>.
    /// </summary>
    public IReadOnlyList<string> FilterParameterNames { get; init; } = ["$filter", "filter"];

    /// <summary>
    /// The maximum value the caller may supply for <c>$top</c>. When a request exceeds this
    /// limit the value is silently clamped to <see cref="MaxPageSize"/>.
    /// <c>null</c> means no upper limit is enforced. Default: <c>null</c>.
    /// </summary>
    public int? MaxPageSize { get; init; }

    /// <summary>
    /// Whether the <c>$orderby</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool OrderByEnabled { get; init; } = true;

    /// <summary>
    /// The query parameter names recognised as the <c>$orderby</c> option, tried left-to-right.
    /// Default: <c>["$orderby", "orderby"]</c>.
    /// </summary>
    public IReadOnlyList<string> OrderByParameterNames { get; init; } = ["$orderby", "orderby"];

    /// <summary>
    /// Whether the <c>$skip</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool SkipEnabled { get; init; } = true;

    /// <summary>
    /// The query parameter names recognised as the <c>$skip</c> option, tried left-to-right.
    /// Default: <c>["$skip", "skip"]</c>.
    /// </summary>
    public IReadOnlyList<string> SkipParameterNames { get; init; } = ["$skip", "skip"];

    /// <summary>
    /// Whether the <c>$skiptoken</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool SkipTokenEnabled { get; init; } = true;

    /// <summary>
    /// The query parameter names recognised as the <c>$skiptoken</c> option, tried left-to-right.
    /// Default: <c>["$skiptoken", "skiptoken"]</c>.
    /// </summary>
    public IReadOnlyList<string> SkipTokenParameterNames { get; init; } = ["$skiptoken", "skiptoken"];

    /// <summary>
    /// String comparison used for filter string comparisons (<c>eq</c>, <c>startsWith</c>, etc.).
    /// Default: <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public StringComparison StringComparison { get; init; } = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Whether the <c>$top</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool TopEnabled { get; init; } = true;

    /// <summary>
    /// The query parameter names recognised as the <c>$top</c> option, tried left-to-right.
    /// Default: <c>["$top", "top"]</c>.
    /// </summary>
    public IReadOnlyList<string> TopParameterNames { get; init; } = ["$top", "top"];

    /// <summary>
    /// Returns a new <see cref="ApiQueryOptionsSettings"/> with <see cref="MaxPageSize"/> and/or
    /// <see cref="DefaultPageSize"/> overridden. All other settings are copied unchanged.
    /// A <c>null</c> argument leaves the corresponding value from the current instance.
    /// </summary>
    /// <param name="maxPageSize">
    /// Override for <see cref="MaxPageSize"/>, or <c>null</c> to keep the current value.
    /// </param>
    /// <param name="defaultPageSize">
    /// Override for <see cref="DefaultPageSize"/>, or <c>null</c> to keep the current value.
    /// </param>
    public ApiQueryOptionsSettings WithPageSizeOverrides(int? maxPageSize, int? defaultPageSize)
    {
        return new ApiQueryOptionsSettings
        {
            DefaultPageSize = defaultPageSize ?? DefaultPageSize,
            ExpandEnabled = ExpandEnabled,
            ExpandParameterNames = ExpandParameterNames,
            FilterEnabled = FilterEnabled,
            FilterParameterNames = FilterParameterNames,
            MaxPageSize = maxPageSize ?? MaxPageSize,
            OrderByEnabled = OrderByEnabled,
            OrderByParameterNames = OrderByParameterNames,
            SkipEnabled = SkipEnabled,
            SkipParameterNames = SkipParameterNames,
            SkipTokenEnabled = SkipTokenEnabled,
            SkipTokenParameterNames = SkipTokenParameterNames,
            StringComparison = StringComparison,
            TopEnabled = TopEnabled,
            TopParameterNames = TopParameterNames,
        };
    }
}