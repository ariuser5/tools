namespace DCiuve.Shared.Logging;

/// <summary>
/// A simple console logger with colored output and configurable verbosity.
/// </summary>
public class Logger : ILogger
{
    /// <summary>
    /// Gets or sets the minimum log level that will be output.
    /// </summary>
    public LogLevel Verbosity { get; set; } = LogLevel.Info;

    /// <summary>
    /// Logs an error message in red.
    /// </summary>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional formatting arguments.</param>
    public void Error(string message, params object[] args)
    {
        if (Verbosity >= LogLevel.Error)
        {
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            WriteColoredLine($"[ERROR] {formattedMessage}", ConsoleColor.Red);
        }
    }

    /// <summary>
    /// Logs a warning message in yellow.
    /// </summary>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional formatting arguments.</param>
    public void Warning(string message, params object[] args)
    {
        if (Verbosity >= LogLevel.Warning)
        {
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            WriteColoredLine($"[WARN]  {formattedMessage}", ConsoleColor.Yellow);
        }
    }

    /// <summary>
    /// Logs an informational message in white.
    /// </summary>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional formatting arguments.</param>
    public void Info(string message, params object[] args)
    {
        if (Verbosity >= LogLevel.Info)
        {
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            WriteColoredLine($"[INFO]  {formattedMessage}", ConsoleColor.White);
        }
    }

    /// <summary>
    /// Logs a debug message in gray.
    /// </summary>
    /// <param name="message">The message template.</param>
    /// <param name="args">Optional formatting arguments.</param>
    public void Debug(string message, params object[] args)
    {
        if (Verbosity >= LogLevel.Debug)
        {
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            WriteColoredLine($"[DEBUG] {formattedMessage}", ConsoleColor.Gray);
        }
    }
    
    public void ConfigureWithOptions(ILogVerbosityOptions options)
    {
        Verbosity = options.Verbosity;
    }

    /// <summary>
    /// Writes a colored line to the console, preserving the original color.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <param name="color">The color to use for the message.</param>
    private static void WriteColoredLine(string message, ConsoleColor color)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }
}
