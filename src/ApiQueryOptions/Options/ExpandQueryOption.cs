namespace ApiQueryOptions.Options;

/// <summary>
/// Parsed representation of the <c>$expand</c> query parameter.
/// Value is a comma-delimited list of navigation property paths (dot-notation supported).
/// </summary>
public sealed class ExpandQueryOption
{
    /// <summary>
    /// Initializes a new instance by parsing the comma-delimited <paramref name="rawValue"/>.
    /// </summary>
    /// <param name="rawValue">The raw <c>$expand</c> query string value.</param>
    public ExpandQueryOption(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            NavigationProperties = Array.Empty<string>();
            return;
        }

        NavigationProperties = rawValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// The ordered list of navigation property paths to expand.
    /// </summary>
    public IReadOnlyList<string> NavigationProperties { get; }
}