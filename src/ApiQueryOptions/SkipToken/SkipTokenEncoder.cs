using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ApiQueryOptions.SkipToken;

/// <summary>
/// Encodes and decodes a skip token — a Base64URL-encoded JSON blob that captures
/// the current query state for cursor-based pagination.
/// The token uses fixed internal keys, so it decodes correctly regardless of the
/// <c>*ParameterNames</c> configured in <see cref="ApiQueryOptionsSettings"/>.
/// </summary>
public static class SkipTokenEncoder
{
    private const string ExpandKey = "expand";
    private const string FilterKey = "filter";
    private const string OrderByKey = "orderby";
    private const string SkipKey = "skip";
    private const string TopKey = "top";

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

        ApiQueryOptionsSettings resolved = settings ?? new ApiQueryOptionsSettings();
        var payload = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string? value) in dict)
        {
            payload[key] = value;
        }

        // Translate the fixed token keys into the first configured name for each option, so the
        // ApiQueryOptions constructor finds them no matter which parameter names are in use.
        // Only known keys are copied, which also keeps a nested skip token out of the result.
        var queryDict = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        CopyValue(payload, ExpandKey, resolved.ExpandParameterNames, queryDict);
        CopyValue(payload, FilterKey, resolved.FilterParameterNames, queryDict);
        CopyValue(payload, OrderByKey, resolved.OrderByParameterNames, queryDict);
        CopyValue(payload, SkipKey, resolved.SkipParameterNames, queryDict);
        CopyValue(payload, TopKey, resolved.TopParameterNames, queryDict);

        return new ApiQueryOptions<T>(new QueryCollection(queryDict), resolved);
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
            dict[FilterKey] = options.Filter.RawValue;
        }

        if (options.Expand is not null)
        {
            dict[ExpandKey] = string.Join(",", options.Expand.NavigationProperties);
        }

        if (options.OrderBy is not null)
        {
            dict[OrderByKey] = string.Join(",",
            options.OrderBy.Items.Select(i => i.Descending ? $"{i.Property} desc" : $"{i.Property} asc"));
        }

        if (options.Top is not null)
        {
            dict[TopKey] = options.Top.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (skipOverride.HasValue)
        {
            dict[SkipKey] = skipOverride.Value.ToString(CultureInfo.InvariantCulture);
        }
        else if (options.Skip is not null)
        {
            dict[SkipKey] = options.Skip.Value.ToString(CultureInfo.InvariantCulture);
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

    private static void CopyValue(
        Dictionary<string, string?> payload,
        string tokenKey,
        IReadOnlyList<string>? parameterNames,
        Dictionary<string, StringValues> target)
    {
        if (payload.TryGetValue(tokenKey, out string? value) && value is not null && parameterNames is { Count: > 0 })
        {
            target[parameterNames[0]] = new StringValues(value);
        }
    }
}
