namespace ApiQueryOptions;

/// <summary>
/// Controls how <c>$filter</c> compares strings in <c>eq</c>, <c>ne</c>, <c>lt</c>, <c>gt</c>,
/// <c>le</c>, <c>ge</c>, <c>startswith</c>, <c>endswith</c>, and <c>contains</c>.
/// </summary>
public enum StringMatching
{
    /// <summary>
    /// Case-insensitive and culture-invariant. In-memory sources use
    /// <see cref="StringComparison.OrdinalIgnoreCase"/>. Database providers compare
    /// <c>UPPER(column)</c> with a value upper-cased on the client, which stops the database
    /// from using an index on the column. This is the default.
    /// </summary>
    IgnoreCase = 0,

    /// <summary>
    /// Case-sensitive. In-memory sources use <see cref="StringComparison.Ordinal"/>.
    /// Database providers receive a plain comparison, so the result is case-sensitive only when
    /// the column's collation is.
    /// </summary>
    CaseSensitive = 1,

    /// <summary>
    /// Plain <c>==</c>, <c>string.Compare</c>, <c>StartsWith</c>, <c>EndsWith</c>, and
    /// <c>Contains</c> calls with nothing added, so the database collation decides case
    /// sensitivity and indexes can be used. Combine with a case-insensitive collation for fast
    /// case-insensitive matching. In-memory sources use the default .NET behavior of each call.
    /// </summary>
    Database = 2,
}
