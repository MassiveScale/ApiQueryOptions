namespace ApiQueryOptions.Filter;

/// <summary>
/// Comparison operators supported in <c>$filter</c> expressions.
/// </summary>
public enum FilterOperator
{
    /// <summary>
    /// Equal to (<c>eq</c>).
    /// </summary>
    Eq,

    /// <summary>
    /// Not equal to (<c>ne</c>).
    /// </summary>
    Ne,

    /// <summary>
    /// Less than (<c>lt</c>).
    /// </summary>
    Lt,

    /// <summary>
    /// Greater than (<c>gt</c>).
    /// </summary>
    Gt,

    /// <summary>
    /// Less than or equal to (<c>le</c>).
    /// </summary>
    Le,

    /// <summary>
    /// Greater than or equal to (<c>ge</c>).
    /// </summary>
    Ge
}