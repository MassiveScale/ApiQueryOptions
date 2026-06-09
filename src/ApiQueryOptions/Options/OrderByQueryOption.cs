namespace ApiQueryOptions.Options;

/// <summary>
/// Parsed representation of the <c>$orderby</c> query parameter.
/// Value is a comma-delimited list of <c>property [asc|desc]</c> expressions.
/// </summary>
public sealed class OrderByQueryOption
{
    /// <summary>
    /// Initializes a new instance by parsing the comma-delimited <paramref name="rawValue"/>.
    /// </summary>
    /// <param name="rawValue">The raw <c>$orderby</c> query string value.</param>
    public OrderByQueryOption(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            Items = Array.Empty<OrderByItem>();
            return;
        }

        var items = new List<OrderByItem>();
        foreach (string segment in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string property = parts[0];
            bool descending = parts.Length > 1 &&
                             string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase);
            items.Add(new OrderByItem(property, descending));
        }

        Items = items.AsReadOnly();
    }

    /// <summary>
    /// The ordered list of sort items.
    /// </summary>
    public IReadOnlyList<OrderByItem> Items { get; }
}