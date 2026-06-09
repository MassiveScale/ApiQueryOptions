using ApiQueryOptions.Options;
using ApiQueryOptions.SkipToken;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions;

/// <summary>
/// Strongly-typed container for OData-style query parameters parsed from an HTTP request.
/// </summary>
/// <typeparam name="T">The entity type being queried.</typeparam>
public sealed class ApiQueryOptions<T>
{
    /// <summary>
    /// Parses query options from an <see cref="IQueryCollection"/>.
    /// Query keys are matched case-insensitively. Unrecognised keys are silently ignored.
    /// Disabled options whose keys are present are silently skipped (no exception at parse time).
    /// </summary>
    public ApiQueryOptions(IQueryCollection query, ApiQueryOptionsSettings? settings = null)
    {
        Settings = settings ?? new ApiQueryOptionsSettings();

        if (query is null)
        {
            return;
        }

        // $skiptoken — when present it fully encodes the query state and overrides all
        // individual URL parameters ($filter, $orderby, $top, $skip, $expand).
        if (Settings.SkipTokenEnabled)
        {
            string? tokenRaw = GetValue(query, "$skiptoken") ?? GetValue(query, "skiptoken");
            if (!string.IsNullOrWhiteSpace(tokenRaw))
            {
                SkipToken = new SkipTokenQueryOption(tokenRaw);
                ApiQueryOptions<T> decoded = SkipTokenEncoder.Decode<T>(tokenRaw, Settings);
                Filter = decoded.Filter;
                Expand = decoded.Expand;
                OrderBy = decoded.OrderBy;
                Top = decoded.Top;
                Skip = decoded.Skip;
                return;
            }
        }

        // $filter
        if (Settings.FilterEnabled)
        {
            string? raw = GetValue(query, "$filter") ?? GetValue(query, "filter");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                Filter = new FilterQueryOption(raw);
            }
        }

        // $expand
        if (Settings.ExpandEnabled)
        {
            string? raw = GetValue(query, "$expand") ?? GetValue(query, "expand");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                Expand = new ExpandQueryOption(raw);
            }
        }

        // $orderby
        if (Settings.OrderByEnabled)
        {
            string? raw = GetValue(query, "$orderby") ?? GetValue(query, "orderby");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                OrderBy = new OrderByQueryOption(raw);
            }
        }

        // $top
        if (Settings.TopEnabled)
        {
            string? raw = GetValue(query, "$top") ?? GetValue(query, "top");
            Top = TopQueryOption.TryParse(raw);

            if (Top is null && Settings.DefaultPageSize.HasValue)
            {
                Top = new TopQueryOption(Settings.DefaultPageSize.Value);
            }

            if (Top is not null && Settings.MaxPageSize.HasValue && Top.Value > Settings.MaxPageSize.Value)
            {
                Top = new TopQueryOption(Settings.MaxPageSize.Value);
            }
        }

        // $skip
        if (Settings.SkipEnabled)
        {
            string? raw = GetValue(query, "$skip") ?? GetValue(query, "skip");
            Skip = SkipQueryOption.TryParse(raw);
        }
    }

    /// <summary>
    /// Parsed <c>$expand</c> option, or <c>null</c> if absent or disabled.
    /// </summary>
    public ExpandQueryOption? Expand { get; }

    /// <summary>
    /// Parsed <c>$filter</c> option, or <c>null</c> if absent or disabled.
    /// </summary>
    public FilterQueryOption? Filter { get; }

    /// <summary>
    /// Parsed <c>$orderby</c> option, or <c>null</c> if absent or disabled.
    /// </summary>
    public OrderByQueryOption? OrderBy { get; }

    /// <summary>
    /// The settings that control which options are accepted. Never <c>null</c>.
    /// </summary>
    public ApiQueryOptionsSettings Settings { get; }

    /// <summary>
    /// Parsed <c>$skip</c> option, or <c>null</c> if absent or disabled.
    /// </summary>
    public SkipQueryOption? Skip { get; }

    /// <summary>
    /// Parsed <c>$skiptoken</c> option, or <c>null</c> if absent or disabled.
    /// </summary>
    public SkipTokenQueryOption? SkipToken { get; }

    /// <summary>
    /// Parsed <c>$top</c> option, or <c>null</c> if absent or disabled.
    /// </summary>
    public TopQueryOption? Top { get; }

    /// <summary>
    /// Generates a Base64URL-encoded skip token for the <em>next</em> page of results,
    /// or <c>null</c> when pagination is not active or no further pages exist.
    /// The encoded token advances <c>$skip</c> by <c>$top</c> so that decoding it
    /// directly yields ready-to-use options for the following page.
    /// </summary>
    /// <param name="resultCount">
    /// The number of items returned by the current query. When this is less than
    /// <c>$top</c> the caller is on the last page and <c>null</c> is returned.
    /// </param>
    /// <param name="totalCount">
    /// The total number of matching records, when known. When provided, the token is
    /// suppressed if the advanced skip cursor would meet or exceed the total.
    /// </param>
    /// <returns>
    /// A URL-safe Base64 skip token whose decoded <c>$skip</c> equals
    /// <c>current skip + top</c>, or <c>null</c> if no next page exists.
    /// </returns>
    public string? NextLink(int resultCount, int? totalCount = null)
    {
        if (Top is null)
        {
            return null;
        }

        int currentSkip = Skip?.Value ?? 0;
        int nextSkip = currentSkip + Top.Value;

        if (resultCount < Top.Value)
        {
            return null;
        }

        if (totalCount.HasValue && nextSkip >= totalCount.Value)
        {
            return null;
        }

        return SkipTokenEncoder.Encode(this, nextSkip);
    }

    /// <summary>
    /// Creates an <see cref="ApiQueryOptions{T}"/> from an <see cref="HttpRequest"/>.
    /// </summary>
    public static ApiQueryOptions<T> FromRequest(HttpRequest request, ApiQueryOptionsSettings? settings = null)
        => new(request.Query, settings);

    private static string? GetValue(IQueryCollection query, string key)
    {
        foreach (string k in query.Keys)
        {
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                StringValues val = query[k];
                return val.Count > 0 ? (string?)val[0] : null;
            }
        }
        return null;
    }
}