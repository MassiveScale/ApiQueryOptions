namespace ApiQueryOptions;

/// <summary>
/// Mutable configuration builder for <see cref="ApiQueryOptionsSettings"/>.
/// Pass an <c>Action&lt;ApiQueryOptionsBuilder&gt;</c> to
/// <see cref="Extensions.ServiceCollectionExtensions.AddApiQueryOptions(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{ApiQueryOptionsBuilder})"/>
/// to configure options inline without constructing the immutable settings record directly.
/// </summary>
public sealed class ApiQueryOptionsBuilder
{
    /// <summary>
    /// The default number of items to return when the caller omits <c>$top</c>.
    /// <c>null</c> means no default is applied and unbounded results are returned when
    /// <c>$top</c> is absent. Default: <c>null</c>.
    /// </summary>
    public int? DefaultPageSize { get; set; }

    /// <summary>
    /// Whether the <c>$expand</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool ExpandEnabled { get; set; } = true;

    /// <summary>
    /// The query parameter names recognised as the <c>$expand</c> option, tried left-to-right.
    /// Default: <c>["$expand", "expand"]</c>.
    /// </summary>
    public IReadOnlyList<string> ExpandParameterNames { get; set; } = ["$expand", "expand"];

    /// <summary>
    /// Whether the <c>$filter</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool FilterEnabled { get; set; } = true;

    /// <summary>
    /// The query parameter names recognised as the <c>$filter</c> option, tried left-to-right.
    /// Default: <c>["$filter", "filter"]</c>.
    /// </summary>
    public IReadOnlyList<string> FilterParameterNames { get; set; } = ["$filter", "filter"];

    /// <summary>
    /// The maximum value the caller may supply for <c>$top</c>. When a request exceeds this
    /// limit the value is silently clamped to <see cref="MaxPageSize"/>.
    /// <c>null</c> means no upper limit is enforced. Default: <c>null</c>.
    /// </summary>
    public int? MaxPageSize { get; set; }

    /// <summary>
    /// Whether the <c>$orderby</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool OrderByEnabled { get; set; } = true;

    /// <summary>
    /// The query parameter names recognised as the <c>$orderby</c> option, tried left-to-right.
    /// Default: <c>["$orderby", "orderby"]</c>.
    /// </summary>
    public IReadOnlyList<string> OrderByParameterNames { get; set; } = ["$orderby", "orderby"];

    /// <summary>
    /// Whether the <c>$skip</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool SkipEnabled { get; set; } = true;

    /// <summary>
    /// The query parameter names recognised as the <c>$skip</c> option, tried left-to-right.
    /// Default: <c>["$skip", "skip"]</c>.
    /// </summary>
    public IReadOnlyList<string> SkipParameterNames { get; set; } = ["$skip", "skip"];

    /// <summary>
    /// Whether the <c>$skiptoken</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool SkipTokenEnabled { get; set; } = true;

    /// <summary>
    /// The query parameter names recognised as the <c>$skiptoken</c> option, tried left-to-right.
    /// Default: <c>["$skiptoken", "skiptoken"]</c>.
    /// </summary>
    public IReadOnlyList<string> SkipTokenParameterNames { get; set; } = ["$skiptoken", "skiptoken"];

    /// <summary>
    /// String comparison used for filter string comparisons (<c>eq</c>, <c>startsWith</c>, etc.).
    /// Default: <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    public StringComparison StringComparison { get; set; } = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Whether the <c>$top</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool TopEnabled { get; set; } = true;

    /// <summary>
    /// The query parameter names recognised as the <c>$top</c> option, tried left-to-right.
    /// Default: <c>["$top", "top"]</c>.
    /// </summary>
    public IReadOnlyList<string> TopParameterNames { get; set; } = ["$top", "top"];

    /// <summary>
    /// Builds an immutable <see cref="ApiQueryOptionsSettings"/> from the current builder state.
    /// </summary>
    internal ApiQueryOptionsSettings Build() =>
        new()
        {
            DefaultPageSize = DefaultPageSize,
            ExpandEnabled = ExpandEnabled,
            ExpandParameterNames = ExpandParameterNames,
            FilterEnabled = FilterEnabled,
            FilterParameterNames = FilterParameterNames,
            MaxPageSize = MaxPageSize,
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
