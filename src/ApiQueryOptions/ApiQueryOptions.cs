using System.Globalization;
using System.Text;
using ApiQueryOptions.Options;
using ApiQueryOptions.SkipToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions;

/// <summary>
/// Strongly-typed container for OData-style query parameters parsed from an HTTP request.
/// </summary>
/// <typeparam name="T">The entity type being queried.</typeparam>
public sealed class ApiQueryOptions<T>
{
    private readonly HttpRequest? httpRequest;
    private readonly IReadOnlySet<string> ownedQueryParams;

    /// <summary>
    /// Parses query options from an <see cref="IQueryCollection"/>.
    /// Query keys are looked up with the collection's own key comparison, which is
    /// case-insensitive for <see cref="HttpRequest.Query"/>. Unrecognised keys are silently ignored.
    /// Disabled options whose keys are present are silently skipped (no exception at parse time).
    /// </summary>
    public ApiQueryOptions(IQueryCollection? query, ApiQueryOptionsSettings? settings = null)
        : this(query, settings, request: null)
    {
    }

    private ApiQueryOptions(IQueryCollection? query, ApiQueryOptionsSettings? settings, HttpRequest? request)
    {
        Settings = settings ?? new ApiQueryOptionsSettings();
        ownedQueryParams = Settings.GetOwnedParameterNames(nameof(settings));
        httpRequest = request;

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
                Filter = new FilterQueryOption(raw, Settings);
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
    /// Returns <c>true</c> when <c>$expand</c> requests <paramref name="propertyPath"/>, and
    /// <c>false</c> when it does not or <c>$expand</c> is absent or disabled.
    /// See <see cref="ExpandQueryOption.Includes(string)"/> for the matching rules.
    /// </summary>
    /// <param name="propertyPath">A property name, or a dot-separated path such as <c>Orders.Items</c>.</param>
    /// <exception cref="ArgumentException">When <paramref name="propertyPath"/> is <c>null</c>, empty, or white space.</exception>
    public bool Expands(string propertyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);

        return Expand?.Includes(propertyPath) ?? false;
    }

    /// <summary>
    /// Creates an <see cref="ApiQueryOptions{T}"/> from an <see cref="HttpRequest"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "T is always required and meaningful at the call site; ApiQueryOptions<Product>.FromRequest(request) is the intended pattern.")]
    public static ApiQueryOptions<T> FromRequest(HttpRequest request, ApiQueryOptionsSettings? settings = null)
        => new(request.Query, settings, request);

    /// <summary>
    /// Creates an <see cref="ApiQueryOptions{T}"/> from an <see cref="HttpContext"/>.
    /// When <paramref name="context"/> is <c>null</c>, returns an empty instance with no parsed options.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "T is always required and meaningful at the call site; ApiQueryOptions<Product>.FromRequest(context) is the intended pattern.")]
    public static ApiQueryOptions<T> FromRequest(HttpContext? context, ApiQueryOptionsSettings? settings = null)
        => new(context?.Request.Query, settings, context?.Request);

    /// <summary>
    /// Creates an <see cref="ApiQueryOptions{T}"/> from an <see cref="IHttpContextAccessor"/>.
    /// When <see cref="IHttpContextAccessor.HttpContext"/> is <c>null</c>, returns an empty instance with no parsed options.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "T is always required and meaningful at the call site; ApiQueryOptions<Product>.FromRequest(accessor) is the intended pattern.")]
    public static ApiQueryOptions<T> FromRequest(IHttpContextAccessor accessor, ApiQueryOptionsSettings? settings = null)
    {
        HttpContext? context = accessor.HttpContext;
        return new(context?.Request.Query, settings, context?.Request);
    }

    /// <summary>
    /// Generates a Base64URL-encoded skip token for paginating to the next page,
    /// or <c>null</c> when pagination is not active, skip tokens are disabled
    /// (<see cref="ApiQueryOptionsSettings.SkipTokenEnabled"/> is <c>false</c>), or no further pages exist.
    /// This method returns only the token itself, not a full URL.
    /// To build a complete next-page URL, use <see cref="NextLink(int, int?)"/> (for instances
    /// created via FromRequest) or <see cref="NextLink(HttpRequest, int, int?)"/>.
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
    public string? GetNextToken(int resultCount, int? totalCount = null)
    {
        if (!Settings.SkipTokenEnabled || !TryGetNextSkip(resultCount, totalCount, out int nextSkip))
        {
            return null;
        }

        return SkipTokenEncoder.Encode(this, nextSkip);
    }

    /// <summary>
    /// Generates a full next-page URL with query parameters, or <c>null</c> when pagination
    /// is not active or no further pages exist.
    /// This overload requires that the instance was created from an <see cref="HttpRequest"/>
    /// (via <see cref="FromRequest(HttpRequest, ApiQueryOptionsSettings?)"/>,
    /// <see cref="FromRequest(HttpContext, ApiQueryOptionsSettings?)"/>,
    /// <see cref="FromRequest(IHttpContextAccessor, ApiQueryOptionsSettings?)"/>, or model binding).
    /// When the instance has no request, the bare skip token is returned instead of a URL, or
    /// <c>null</c> when skip tokens are disabled.
    /// </summary>
    /// <param name="resultCount">
    /// The number of items returned by the current query. When this is less than
    /// <c>$top</c> the caller is on the last page and <c>null</c> is returned.
    /// </param>
    /// <param name="totalCount">
    /// The total number of matching records, when known. When provided, the link is
    /// suppressed if the advanced skip cursor would meet or exceed the total.
    /// </param>
    /// <returns>
    /// An absolute next-page URL (see <see cref="NextLink(HttpRequest, int, int?)"/> for its shape),
    /// or <c>null</c> if no next page exists.
    /// </returns>
    public string? NextLink(int resultCount, int? totalCount = null)
    {
        if (httpRequest is null)
        {
            return GetNextToken(resultCount, totalCount);
        }

        return NextLink(httpRequest, resultCount, totalCount);
    }

    /// <summary>
    /// Builds a full next-page URL from the given <paramref name="request"/> URI,
    /// or <c>null</c> when pagination is not active or no further pages exist.
    /// When <see cref="ApiQueryOptionsSettings.SkipTokenEnabled"/> is <c>true</c>, the
    /// ApiQueryOptions-owned parameters (<c>$filter</c>, <c>$orderby</c>, <c>$top</c>,
    /// <c>$skip</c>, <c>$expand</c>, <c>$skiptoken</c>, and their configured variants)
    /// are removed from the query string and replaced by a single <c>$skiptoken</c>.
    /// When skip tokens are disabled, only the paging parameters are replaced, by explicit
    /// <c>$top</c> and <c>$skip</c> values; <c>null</c> is returned if <c>$skip</c> is also disabled,
    /// because the next page could not be requested.
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
    /// An absolute URL with non-ApiQueryOptions parameters forwarded and the next-page
    /// parameters appended, or <c>null</c> if no next page exists.
    /// </returns>
    public string? NextLink(HttpRequest request, int resultCount, int? totalCount = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Settings.SkipTokenEnabled && !Settings.SkipEnabled)
        {
            return null;
        }

        if (!TryGetNextSkip(resultCount, totalCount, out int nextSkip))
        {
            return null;
        }

        return BuildNextLinkUrl(request, nextSkip);
    }

    private static string? GetFirstValue(IQueryCollection query, IReadOnlyList<string> names) =>
        names.Select(name => GetValue(query, name)).FirstOrDefault(value => value is not null);

    private static string? GetValue(IQueryCollection query, string key) =>
        query.TryGetValue(key, out StringValues values) && values.Count > 0 ? values[0] : null;

    private static bool IsNamed(string key, IReadOnlyList<string> names) =>
        names.Any(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase));

    private static void AppendQueryParameter(StringBuilder query, string name, string? value)
    {
        query.Append(query.Length == 0 ? '?' : '&');
        query.Append(QueryStringEncoder.EncodeName(name));
        query.Append('=');
        query.Append(Uri.EscapeDataString(value ?? string.Empty));
    }

    private string BuildNextLinkUrl(HttpRequest request, int nextSkip)
    {
        var query = new StringBuilder();

        foreach (KeyValuePair<string, StringValues> kvp in request.Query)
        {
            if (IsReplacedInNextLink(kvp.Key))
            {
                continue;
            }

            foreach (string? value in kvp.Value)
            {
                // Append all non-ApiQueryOptions parameters unchanged,
                // including any unrecognized or custom ones. These are
                // forwarded verbatim since we cannot know whether they
                // are relevant endpoint handling the next page or not.
                AppendQueryParameter(query, kvp.Key, value);
            }
        }

        if (Settings.SkipTokenEnabled)
        {
            AppendQueryParameter(query, Settings.SkipTokenParameterNames[0], SkipTokenEncoder.Encode(this, nextSkip));
        }
        else
        {
            AppendQueryParameter(query, Settings.TopParameterNames[0], Top!.Value.ToString(CultureInfo.InvariantCulture));
            AppendQueryParameter(query, Settings.SkipParameterNames[0], nextSkip.ToString(CultureInfo.InvariantCulture));
        }

        return UriHelper.BuildAbsolute(
            request.Scheme,
            request.Host,
            request.PathBase,
            request.Path,
            new QueryString(query.ToString()));
    }

    private bool IsReplacedInNextLink(string key)
    {
        // A skip token fully encodes the query state, so every owned parameter is replaced
        // (they would be ignored anyway). Offset links only change the paging parameters and
        // forward $filter, $orderby, and $expand exactly as the client sent them.
        if (Settings.SkipTokenEnabled)
        {
            return ownedQueryParams.Contains(key);
        }

        return IsNamed(key, Settings.TopParameterNames)
            || IsNamed(key, Settings.SkipParameterNames)
            || IsNamed(key, Settings.SkipTokenParameterNames);
    }

    private bool TryGetNextSkip(int resultCount, int? totalCount, out int nextSkip)
    {
        nextSkip = 0;

        if (Top is null || resultCount < Top.Value)
        {
            return false;
        }

        long advanced = (long)(Skip?.Value ?? 0) + Top.Value;
        if (advanced > int.MaxValue)
        {
            return false;
        }

        if (totalCount.HasValue && advanced >= totalCount.Value)
        {
            return false;
        }

        nextSkip = (int)advanced;
        return true;
    }
}
