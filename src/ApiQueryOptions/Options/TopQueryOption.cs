namespace ApiQueryOptions.Options;

/// <summary>Parsed representation of the <c>$top</c> query parameter.</summary>
public sealed class TopQueryOption
{
    /// <summary>
    /// Initializes a new instance with the given limit.
    /// </summary>
    /// <param name="value">The maximum number of items to return. Must be a positive integer.</param>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="value"/> is zero or negative.</exception>
    public TopQueryOption(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>
    /// The maximum number of items to return.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Parses a raw query string value. Returns <c>null</c> if the value is not a positive integer,
    /// so zero, negative, and non-numeric values are ignored like an absent <c>$top</c>.
    /// </summary>
    public static TopQueryOption? TryParse(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return int.TryParse(rawValue, out int n) && n > 0 ? new TopQueryOption(n) : null;
    }
}
