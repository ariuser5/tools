namespace DCiuve.Tools.Logging;

/// <summary>
/// Interface for logging functionality.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Gets or sets the minimum log level that will be output.
    /// </summary>
    LogLevel Verbosity { get; set; }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional formatting arguments.</param>
    void Error(string message, params object[] args);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional formatting arguments.</param>
    void Warning(string message, params object[] args);

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional formatting arguments.</param>
    void Info(string message, params object[] args);

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional formatting arguments.</param>
    void Debug(string message, params object[] args);
}
