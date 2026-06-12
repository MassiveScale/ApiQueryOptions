using System.Text;
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
    private readonly HashSet<string> _ownedQueryParams;

    /// <summary>
    /// Parses query options from an <see cref="IQueryCollection"/>.
    /// Query keys are matched case-insensitively. Unrecognised keys are silently ignored.
    /// Disabled options whose keys are present are silently skipped (no exception at parse time).
    /// </summary>
    public ApiQueryOptions(IQueryCollection? query, ApiQueryOptionsSettings? settings = null)
    {
        Settings = settings ?? new ApiQueryOptionsSettings();

        _ownedQueryParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in Settings.ExpandParameterNames) _ownedQueryParams.Add(name);
        foreach (string name in Settings.FilterParameterNames) _ownedQueryParams.Add(name);
        foreach (string name in Settings.OrderByParameterNames) _ownedQueryParams.Add(name);
        foreach (string name in Settings.SkipParameterNames) _ownedQueryParams.Add(name);
        foreach (string name in Settings.SkipTokenParameterNames) _ownedQueryParams.Add(name);
        foreach (string name in Settings.TopParameterNames) _ownedQueryParams.Add(name);

        if (query is null)
        {
            return;
        }

        // $skiptoken — when present it fully encodes the query state and overrides all
        // individual URL parameters ($filter, $orderby, $top, $skip, $expand).
        if (Settings.SkipTokenEnabled)
        {
            string? tokenRaw = GetFirstValue(query, Settings.SkipTokenParameterNames);
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
            string? raw = GetFirstValue(query, Settings.FilterParameterNames);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                Filter = new FilterQueryOption(raw);
            }
        }

        // $expand
        if (Settings.ExpandEnabled)
        {
            string? raw = GetFirstValue(query, Settings.ExpandParameterNames);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                Expand = new ExpandQueryOption(raw);
            }
        }

        // $orderby
        if (Settings.OrderByEnabled)
        {
            string? raw = GetFirstValue(query, Settings.OrderByParameterNames);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                OrderBy = new OrderByQueryOption(raw);
            }
        }

        // $top
        if (Settings.TopEnabled)
        {
            string? raw = GetFirstValue(query, Settings.TopParameterNames);
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
            string? raw = GetFirstValue(query, Settings.SkipParameterNames);
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
    /// Builds a full next-page URL from the given <paramref name="request"/> URI,
    /// or <c>null</c> when pagination is not active or no further pages exist.
    /// ApiQueryOptions-owned parameters (<c>$filter</c>, <c>$orderby</c>, <c>$top</c>,
    /// <c>$skip</c>, <c>$expand</c>, <c>$skiptoken</c>, and their un-prefixed variants)
    /// are removed from the query string and replaced by a single <c>$skiptoken</c>.
    /// All other query parameters are preserved unchanged.
    /// </summary>
    /// <param name="request">
    /// The current HTTP request, used to derive the base URL (scheme, host, path) and
    /// any non-ApiQueryOptions query parameters to forward.
    /// </param>
    /// <param name="resultCount">
    /// The number of items returned by the current query. When this is less than
    /// <c>$top</c> the caller is on the last page and <c>null</c> is returned.
    /// </param>
    /// <param name="totalCount">
    /// The total number of matching records, when known. When provided, the link is
    /// suppressed if the advanced skip cursor would meet or exceed the total.
    /// </param>
    /// <returns>
    /// An absolute URL with non-ApiQueryOptions parameters forwarded and
    /// <c>$skiptoken=…</c> appended, or <c>null</c> if no next page exists.
    /// </returns>
    public string? NextLink(HttpRequest request, int resultCount, int? totalCount = null)
    {
        string? token = NextLink(resultCount, totalCount);
        if (token is null)
        {
            return null;
        }

        var qs = new StringBuilder();
        foreach (KeyValuePair<string, StringValues> kvp in request.Query)
        {
            if (_ownedQueryParams.Contains(kvp.Key))
            {
                continue;
            }

            foreach (string? value in kvp.Value)
            {
                if (qs.Length > 0)
                {
                    qs.Append('&');
                }

                qs.Append(Uri.EscapeDataString(kvp.Key))
                  .Append('=')
                  .Append(Uri.EscapeDataString(value ?? string.Empty));
            }
        }

        if (qs.Length > 0)
        {
            qs.Append('&');
        }

        qs.Append(Settings.SkipTokenParameterNames[0]).Append('=').Append(token);

        return $"{request.Scheme}://{request.Host}{request.Path}?{qs}";
    }

    /// <summary>
    /// Creates an <see cref="ApiQueryOptions{T}"/> from an <see cref="HttpRequest"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "T is always required and meaningful at the call site; ApiQueryOptions<Product>.FromRequest(request) is the intended pattern.")]
    public static ApiQueryOptions<T> FromRequest(HttpRequest request, ApiQueryOptionsSettings? settings = null)
        => new(request.Query, settings);

    /// <summary>
    /// Creates an <see cref="ApiQueryOptions{T}"/> from an <see cref="HttpContext"/>.
    /// When <paramref name="context"/> is <c>null</c>, returns an empty instance with no parsed options.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "T is always required and meaningful at the call site; ApiQueryOptions<Product>.FromRequest(context) is the intended pattern.")]
    public static ApiQueryOptions<T> FromRequest(HttpContext? context, ApiQueryOptionsSettings? settings = null)
        => new(context?.Request.Query, settings);

    /// <summary>
    /// Creates an <see cref="ApiQueryOptions{T}"/> from an <see cref="IHttpContextAccessor"/>.
    /// When <see cref="IHttpContextAccessor.HttpContext"/> is <c>null</c>, returns an empty instance with no parsed options.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "T is always required and meaningful at the call site; ApiQueryOptions<Product>.FromRequest(accessor) is the intended pattern.")]
    public static ApiQueryOptions<T> FromRequest(IHttpContextAccessor accessor, ApiQueryOptionsSettings? settings = null)
        => FromRequest(accessor.HttpContext, settings);

    private static string? GetFirstValue(IQueryCollection query, IReadOnlyList<string> names) =>
        names.Select(name => GetValue(query, name)).FirstOrDefault(value => value is not null);

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

