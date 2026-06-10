using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace ApiQueryOptions.SkipToken;

/// <summary>
/// Encodes and decodes a skip token — a Base64URL-encoded JSON blob that captures
/// the current query state for cursor-based pagination.
/// </summary>
public static class SkipTokenEncoder
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Decodes a skip token string back into an <see cref="ApiQueryOptions{T}"/>.
    /// </summary>
    /// <exception cref="FormatException">Thrown when the token is malformed or cannot be decoded.</exception>
    public static ApiQueryOptions<T> Decode<T>(string token, ApiQueryOptionsSettings? settings = null)
    {
        byte[] bytes;
        try
        {
            bytes = Base64UrlDecode(token);
        }
        catch (Exception ex)
        {
            throw new FormatException($"The skip token is not valid Base64URL: {ex.Message}", ex);
        }

        Dictionary<string, string?>? dict;
        try
        {
            dict = JsonSerializer.Deserialize<Dictionary<string, string?>>(bytes, _jsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"The skip token contains invalid JSON: {ex.Message}", ex);
        }

        if (dict is null)
        {
            throw new FormatException("The skip token decoded to a null payload.");
        }

        // Rebuild from the decoded dictionary using a QueryCollection adapter
        var queryDict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(
            StringComparer.OrdinalIgnoreCase);

        foreach ((string? key, string? value) in dict)
        {
            if (value is not null)
            {
                queryDict[key] = new Microsoft.Extensions.Primitives.StringValues(value);
            }
        }

        var queryCollection = new QueryCollection(queryDict);
        return new ApiQueryOptions<T>(queryCollection, settings);
    }

    /// <summary>
    /// Encodes the relevant query option values from <paramref name="options"/> into
    /// a URL-safe Base64 skip token string.
    /// </summary>
    /// <param name="options">
    /// The query options to encode.
    /// </param>
    /// <param name="skipOverride">
    /// When provided, encodes this value as <c>$skip</c> in the token instead of
    /// <see cref="ApiQueryOptions{T}.Skip"/>. Used by
    /// <see cref="ApiQueryOptions{T}.NextLink(int, int?)"/> to advance the cursor to the next page.
    /// </param>
    public static string Encode<T>(ApiQueryOptions<T> options, int? skipOverride = null)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (options.Filter is not null)
        {
            dict["filter"] = options.Filter.RawValue;
        }

        if (options.Expand is not null)
        {
            dict["expand"] = string.Join(",", options.Expand.NavigationProperties);
        }

        if (options.OrderBy is not null)
        {
            dict["orderby"] = string.Join(",",
            options.OrderBy.Items.Select(i => i.Descending ? $"{i.Property} desc" : $"{i.Property} asc"));
        }

        if (options.Top is not null)
        {
            dict["top"] = options.Top.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (skipOverride.HasValue)
        {
            dict["skip"] = skipOverride.Value.ToString(CultureInfo.InvariantCulture);
        }
        else if (options.Skip is not null)
        {
            dict["skip"] = options.Skip.Value.ToString(CultureInfo.InvariantCulture);
        }

        string json = JsonSerializer.Serialize(dict, _jsonOptions);
        return Base64UrlEncode(Encoding.UTF8.GetBytes(json));
    }

    private static byte[] Base64UrlDecode(string token)
    {
        string s = token.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }

    private static string Base64UrlEncode(byte[] bytes)
            => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}