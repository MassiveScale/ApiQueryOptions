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
    /// Whether the <c>$filter</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool FilterEnabled { get; set; } = true;

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
    /// Whether the <c>$skip</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool SkipEnabled { get; set; } = true;

    /// <summary>
    /// Whether the <c>$skiptoken</c> query parameter is accepted. Default: <c>true</c>.
    /// </summary>
    public bool SkipTokenEnabled { get; set; } = true;

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
    /// Builds an immutable <see cref="ApiQueryOptionsSettings"/> from the current builder state.
    /// </summary>
    internal ApiQueryOptionsSettings Build() =>
        new()
        {
            DefaultPageSize = DefaultPageSize,
            ExpandEnabled = ExpandEnabled,
            FilterEnabled = FilterEnabled,
            MaxPageSize = MaxPageSize,
            OrderByEnabled = OrderByEnabled,
            SkipEnabled = SkipEnabled,
            SkipTokenEnabled = SkipTokenEnabled,
            StringComparison = StringComparison,
            TopEnabled = TopEnabled,
        };
}
