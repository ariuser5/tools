namespace DCiuve.Tools.Gcp.PubSub.Cli;

public enum LogLevel
{
    Error = 0,
    Warning = 1,
    Info = 2,
    Debug = 3
}

public class Logger
{
    public LogLevel Verbosity { get; set; } = LogLevel.Info;

    public void Error(string message, params object[] args)
    {
        if (Verbosity >= LogLevel.Error)
        {
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            WriteColoredLine($"[ERROR] {formattedMessage}", ConsoleColor.Red);
        }
    }

    public void Warning(string message, params object[] args)
    {
        if (Verbosity >= LogLevel.Warning)
        {
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            WriteColoredLine($"[WARN]  {formattedMessage}", ConsoleColor.Yellow);
        }
    }

    public void Info(string message, params object[] args)
    {
        if (Verbosity >= LogLevel.Info)
        {
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            WriteColoredLine($"[INFO]  {formattedMessage}", ConsoleColor.White);
        }
    }

    public void Debug(string message, params object[] args)
    {
        if (Verbosity >= LogLevel.Debug)
        {
            var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
            WriteColoredLine($"[DEBUG] {formattedMessage}", ConsoleColor.Gray);
        }
    }

    private static void WriteColoredLine(string message, ConsoleColor color)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }
}
