using Microsoft.AspNetCore.Http;

namespace ApiQueryOptions;

/// <summary>
/// Optional wrapper for returning paged collections from API endpoints.
/// </summary>
public sealed class PagedResponse<T>
{
    /// <param name="value">The items in the current page.</param>
    /// <param name="nextLink">Absolute URL to the next page, or <c>null</c>.</param>
    /// <param name="count">Total number of items across all pages, or <c>null</c> if not computed.</param>
    public PagedResponse(IReadOnlyList<T> value, string? nextLink = null, int? count = null)
    {
        Value = value;
        NextLink = nextLink;
        Count = count;
    }

    /// <summary>Total number of items matching the query (across all pages), or <c>null</c> if not computed.</summary>
    public int? Count { get; }

    /// <summary>Absolute URL to retrieve the next page, or <c>null</c> if this is the last page.</summary>
    public string? NextLink { get; }

    /// <summary>The items in the current page.</summary>
    public IReadOnlyList<T> Value { get; }
}

/// <summary>
/// Factory methods for <see cref="PagedResponse{T}"/>.
/// </summary>
public static class PagedResponse
{
    /// <summary>
    /// Creates a <see cref="PagedResponse{T}"/> from the current page of results, computing
    /// the next-page URL from the request URI when more pages exist.
    /// ApiQueryOptions-owned parameters are stripped from the query string and replaced
    /// by a single <c>$skiptoken</c>; all other query parameters are forwarded unchanged.
    /// </summary>
    /// <param name="value">The items in the current page.</param>
    /// <param name="options">The query options used to fetch <paramref name="value"/>.</param>
    /// <param name="request">The current HTTP request, used to build the next-page URL.</param>
    /// <param name="totalCount">Total number of items across all pages, or <c>null</c> if not computed.</param>
    public static PagedResponse<T> Create<T>(
        IReadOnlyList<T> value,
        ApiQueryOptions<T> options,
        HttpRequest request,
        int? totalCount = null)
    {
        string? nextLink = options.NextLink(request, value.Count, totalCount);
        return new PagedResponse<T>(value, nextLink: nextLink, count: totalCount);
    }
}