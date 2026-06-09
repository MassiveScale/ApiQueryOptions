namespace ApiQueryOptions.Filter;

/// <summary>
/// String functions supported in <c>$filter</c> expressions.
/// </summary>
public enum StringFunction
{
    /// <summary>
    /// Tests whether a string property begins with the given value (<c>startswith</c>).
    /// </summary>
    StartsWith,

    /// <summary>
    /// Tests whether a string property ends with the given value (<c>endswith</c>).
    /// </summary>
    EndsWith,

    /// <summary>
    /// Tests whether a string property contains the given substring (<c>contains</c>).
    /// </summary>
    Contains
}