namespace ApiQueryOptions.Options;

/// <summary>Represents a single property in an <c>$orderby</c> clause.</summary>
/// <param name="Property">The property name to order by.</param>
/// <param name="Descending">Whether the sort direction is descending.</param>
public sealed record OrderByItem(string Property, bool Descending);