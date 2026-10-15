namespace ApiQueryOptions.Options;

/// <summary>Parsed representation of the <c>$skip</c> query parameter.</summary>
public sealed class SkipQueryOption
{
    /// <summary>
    /// Initializes a new instance with the given skip count.
    /// </summary>
    /// <param name="value">The number of items to skip. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="value"/> is negative.</exception>
    public SkipQueryOption(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>
    /// The number of items to skip.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Parses a raw query string value. Returns <c>null</c> if the value is not a non-negative
    /// integer, so negative and non-numeric values are ignored like an absent <c>$skip</c>.
    /// </summary>
    public static SkipQueryOption? TryParse(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return int.TryParse(rawValue, out int n) && n >= 0 ? new SkipQueryOption(n) : null;
    }
}
