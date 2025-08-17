using DCiuve.Shared.Logging;

namespace DCiuve.Shared.Cli;

/// <summary>
/// A logger proxy that can suppress non-error messages when in silent mode (--silent).
/// </summary>
public class SuppressibleLogger : ILogger
{
    private readonly ILogger _innerLogger;

    public SuppressibleLogger(ILogger innerLogger)
    {
        _innerLogger = innerLogger ?? throw new ArgumentNullException(nameof(innerLogger));
    }

	public LogLevel Verbosity 
    { 
        get => _innerLogger.Verbosity; 
        set => _innerLogger.Verbosity = value; 
    }
    
    public bool isSilent { get; set; }

    public void Debug(string message, params object[] args)
    {
        if (!isSilent)
        {
            _innerLogger.Debug(message, args);
        }
    }

    public void Info(string message, params object[] args)
    {
        if (!isSilent)
        {
            _innerLogger.Info(message, args);
        }
    }

    public void Warning(string message, params object[] args)
    {
        if (!isSilent)
        {
            _innerLogger.Warning(message, args);
        }
    }

    public void Error(string message, params object[] args)
    {
        // Always show errors, even in quiet mode
        _innerLogger.Error(message, args);
    }
}
