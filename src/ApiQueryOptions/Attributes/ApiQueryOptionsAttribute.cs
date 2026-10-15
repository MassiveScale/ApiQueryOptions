namespace ApiQueryOptions;

/// <summary>
/// Overrides <see cref="ApiQueryOptionsSettings"/> for a single controller class or action method.
/// Values set here take precedence over those registered at startup via
/// <c>AddApiQueryOptions()</c>. When applied to both a controller and one of its methods,
/// the method-level attribute wins.
/// Unset properties (integer <c>0</c> or <see cref="QueryOptionState.Default"/>) are not
/// overridden; the registered setting is used unchanged.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ApiQueryOptionsAttribute : Attribute
{
    /// <summary>
    /// Overrides <see cref="ApiQueryOptionsSettings.DefaultPageSize"/>.
    /// <c>0</c> (the default) leaves the registered value unchanged.
    /// </summary>
    public int DefaultPageSize { get; set; }

    /// <summary>
    /// Overrides <see cref="ApiQueryOptionsSettings.ExpandEnabled"/>.
    /// <see cref="QueryOptionState.Default"/> leaves the registered value unchanged.
    /// </summary>
    public QueryOptionState ExpandEnabled { get; set; }

    /// <summary>
    /// Overrides <see cref="ApiQueryOptionsSettings.FilterEnabled"/>.
    /// <see cref="QueryOptionState.Default"/> leaves the registered value unchanged.
    /// </summary>
    public QueryOptionState FilterEnabled { get; set; }

    /// <summary>
    /// Overrides <see cref="ApiQueryOptionsSettings.MaxPageSize"/>.
    /// <c>0</c> (the default) leaves the registered value unchanged.
    /// </summary>
    public int MaxPageSize { get; set; }

    /// <summary>
    /// Overrides <see cref="ApiQueryOptionsSettings.OrderByEnabled"/>.
    /// <see cref="QueryOptionState.Default"/> leaves the registered value unchanged.
    /// </summary>
    public QueryOptionState OrderByEnabled { get; set; }

    /// <summary>
    /// Overrides <see cref="ApiQueryOptionsSettings.SkipEnabled"/>.
    /// <see cref="QueryOptionState.Default"/> leaves the registered value unchanged.
    /// </summary>
    public QueryOptionState SkipEnabled { get; set; }

    /// <summary>
    /// Overrides <see cref="ApiQueryOptionsSettings.SkipTokenEnabled"/>.
    /// <see cref="QueryOptionState.Default"/> leaves the registered value unchanged.
    /// </summary>
    public QueryOptionState SkipTokenEnabled { get; set; }

    /// <summary>
    /// Overrides <see cref="ApiQueryOptionsSettings.TopEnabled"/>.
    /// <see cref="QueryOptionState.Default"/> leaves the registered value unchanged.
    /// </summary>
    public QueryOptionState TopEnabled { get; set; }

    /// <summary>
    /// Returns a new <see cref="ApiQueryOptionsSettings"/> that is a copy of
    /// <paramref name="settings"/> with the overrides defined by this attribute applied.
    /// Settings this attribute cannot override (such as the <c>*ParameterNames</c> lists) are
    /// copied unchanged.
    /// </summary>
    /// <param name="settings">The base settings to copy and selectively override.</param>
    public ApiQueryOptionsSettings ApplyTo(ApiQueryOptionsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new ApiQueryOptionsSettings
        {
            DefaultPageSize = DefaultPageSize > 0 ? DefaultPageSize : settings.DefaultPageSize,
            ExpandEnabled = Resolve(ExpandEnabled, settings.ExpandEnabled),
            ExpandParameterNames = settings.ExpandParameterNames,
            FilterEnabled = Resolve(FilterEnabled, settings.FilterEnabled),
            FilterParameterNames = settings.FilterParameterNames,
            MaxFilterDepth = settings.MaxFilterDepth,
            MaxFilterNodeCount = settings.MaxFilterNodeCount,
            MaxPageSize = MaxPageSize > 0 ? MaxPageSize : settings.MaxPageSize,
            OrderByEnabled = Resolve(OrderByEnabled, settings.OrderByEnabled),
            OrderByParameterNames = settings.OrderByParameterNames,
            SkipEnabled = Resolve(SkipEnabled, settings.SkipEnabled),
            SkipParameterNames = settings.SkipParameterNames,
            SkipTokenEnabled = Resolve(SkipTokenEnabled, settings.SkipTokenEnabled),
            SkipTokenParameterNames = settings.SkipTokenParameterNames,
            StringComparison = settings.StringComparison,
            TopEnabled = Resolve(TopEnabled, settings.TopEnabled),
            TopParameterNames = settings.TopParameterNames,
        };
    }

    private static bool Resolve(QueryOptionState state, bool fallback)
    {
        return state switch
        {
            QueryOptionState.Enabled => true,
            QueryOptionState.Disabled => false,
            _ => fallback,
        };
    }
}