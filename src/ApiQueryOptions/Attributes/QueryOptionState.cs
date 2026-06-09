namespace ApiQueryOptions;

/// <summary>
/// Three-state value for per-endpoint boolean setting overrides in
/// <see cref="ApiQueryOptionsAttribute"/>. <see cref="Default"/> leaves the setting
/// unchanged from the value registered at startup.
/// </summary>
public enum QueryOptionState
{
    /// <summary>
    /// No override — the value from the registered <see cref="ApiQueryOptionsSettings"/> is used.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Overrides the setting to <c>false</c> (disabled) regardless of the registered value.
    /// </summary>
    Disabled = 1,

    /// <summary>
    /// Overrides the setting to <c>true</c> (enabled) regardless of the registered value.
    /// </summary>
    Enabled = 2,
}