namespace ApiQueryOptions.Options;

/// <summary>
/// Parsed representation of the <c>$skiptoken</c> query parameter.
/// The token is a Base64-encoded JSON blob produced by <see cref="SkipToken.SkipTokenEncoder"/>.
/// </summary>
public sealed class SkipTokenQueryOption
{
    /// <summary>
    /// Initializes a new instance with the given Base64URL-encoded token string.
    /// </summary>
    /// <param name="rawValue">The encoded skip token from the query string.</param>
    public SkipTokenQueryOption(string rawValue)
    {
        RawValue = rawValue;
    }

    /// <summary>
    /// The raw (encoded) skip token value.
    /// </summary>
    public string RawValue { get; }
}