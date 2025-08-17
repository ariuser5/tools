namespace DCiuve.Shared.Logging;

/// <summary>
/// Represents the level of logging detail.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// No logging output.
    /// </summary>
    Quiet = 0,
    
    /// <summary>
    /// Error messages only.
    /// </summary>
    Error = 1,

    /// <summary>
    /// Warning messages and above.
    /// </summary>
    Warning = 2,

    /// <summary>
    /// Informational messages and above.
    /// </summary>
    Info = 3,

    /// <summary>
    /// Debug messages and above (most verbose).
    /// </summary>
    Debug = 4
}
