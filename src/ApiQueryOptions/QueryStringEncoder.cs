namespace ApiQueryOptions;

/// <summary>
/// Encodes query parameter names for next-page links. Values are encoded with
/// <see cref="Uri.EscapeDataString(string)"/>; names keep the characters that are safe in a
/// query component, so <c>$skiptoken</c> and <c>page[size]</c> stay readable.
/// </summary>
internal static class QueryStringEncoder
{
    private static readonly (string Encoded, string Literal)[] _nameLiterals =
    [
        ("%21", "!"),
        ("%24", "$"),
        ("%27", "'"),
        ("%28", "("),
        ("%29", ")"),
        ("%2A", "*"),
        ("%2C", ","),
        ("%2F", "/"),
        ("%3A", ":"),
        ("%3B", ";"),
        ("%3F", "?"),
        ("%40", "@"),
        ("%5B", "["),
        ("%5D", "]"),
    ];

    /// <summary>
    /// Percent-encodes <paramref name="name"/> for use as a query parameter name. Characters
    /// that delimit or change query values (<c>&amp;</c>, <c>=</c>, <c>+</c>, <c>#</c>,
    /// <c>%</c>, and spaces) are always encoded.
    /// </summary>
    public static string EncodeName(string name)
    {
        string escaped = Uri.EscapeDataString(name);
        if (escaped.Length == name.Length)
        {
            return name;
        }

        // EscapeDataString writes a literal '%' as "%25", so these replacements only ever
        // undo escapes it produced for the characters listed above.
        foreach ((string encoded, string literal) in _nameLiterals)
        {
            escaped = escaped.Replace(encoded, literal, StringComparison.Ordinal);
        }

        return escaped;
    }
}
