namespace DCiuve.Tools.Logging;

/// <summary>
/// Represents the level of logging detail.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Error messages only.
    /// </summary>
    Error = 0,

    /// <summary>
    /// Warning messages and above.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Informational messages and above.
    /// </summary>
    Info = 2,

    /// <summary>
    /// Debug messages and above (most verbose).
    /// </summary>
    Debug = 3
}
