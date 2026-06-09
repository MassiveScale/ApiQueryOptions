namespace ApiQueryOptions.Options;

/// <summary>Parsed representation of the <c>$skip</c> query parameter.</summary>
public sealed class SkipQueryOption
{
    /// <summary>
    /// Initializes a new instance with the given skip count.
    /// </summary>
    /// <param name="value">The number of items to skip. Must be non-negative.</param>
    public SkipQueryOption(int value)
    {
        Value = value;
    }

    /// <summary>
    /// The number of items to skip.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Parses a raw query string value. Returns <c>null</c> if the value is not a valid integer.
    /// </summary>
    public static SkipQueryOption? TryParse(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return int.TryParse(rawValue, out int n) ? new SkipQueryOption(n) : null;
    }
}