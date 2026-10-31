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

    /// <summary>
    /// Returns <c>true</c> when <paramref name="propertyPath"/> was requested, either as a path of
    /// its own or as the start of a longer one: <c>$expand=orders.items</c> includes both
    /// <c>Orders</c> and <c>Orders.Items</c>. Comparison is case-insensitive. Use it in a projection
    /// to load a related collection only when the client asked for it.
    /// </summary>
    /// <param name="propertyPath">A property name, or a dot-separated path such as <c>Orders.Items</c>.</param>
    /// <exception cref="ArgumentException">When <paramref name="propertyPath"/> is <c>null</c>, empty, or white space.</exception>
    public bool Includes(string propertyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);

        foreach (string path in NavigationProperties)
        {
            if (path.Length == propertyPath.Length
                ? string.Equals(path, propertyPath, StringComparison.OrdinalIgnoreCase)
                : path.Length > propertyPath.Length
                    && path[propertyPath.Length] == '.'
                    && path.StartsWith(propertyPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}